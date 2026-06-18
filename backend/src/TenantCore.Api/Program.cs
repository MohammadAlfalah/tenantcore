using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using TenantCore.Api.Common;
using TenantCore.Api.Features.Auth;
using TenantCore.Api.Features.Members;
using TenantCore.Api.Features.Projects;
using TenantCore.Api.Features.Tasks;
using TenantCore.Api.Infrastructure.Auth;
using TenantCore.Api.Infrastructure.Data;
using TenantCore.Api.Infrastructure.Tenancy;

// Keep JWT claim names exactly as issued ("sub", "tenant_id", ...) instead of remapping them.
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration --------------------------------------------------------------------------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwtOptions.Secret) || jwtOptions.Secret.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Secret must be configured and at least 32 characters. Set it via configuration or the JWT__SECRET env var.");

// ---- Database -------------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));

// ---- Application services -------------------------------------------------------------------
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IMemberService, MemberService>();

// ---- Authentication & Authorization ---------------------------------------------------------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Configure the bearer validation lazily from IOptions<JwtOptions> so the signing key used to
// VALIDATE tokens is the exact same bound value the JwtTokenService uses to SIGN them — one source
// of truth, regardless of configuration source ordering.
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtAccessor) =>
    {
        var o = jwtAccessor.Value;
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = o.Issuer,
            ValidateAudience = true,
            ValidAudience = o.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Secret)),
            ClockSkew = TimeSpan.FromSeconds(15),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name
        };
    });

builder.Services.AddAuthorizationBuilder().AddTenantCorePolicies();

// ---- MVC / JSON -----------------------------------------------------------------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        // Serialize enums as their names ("Admin", "Todo") for a readable, stable API contract.
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// ---- Error handling -------------------------------------------------------------------------
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ---- CORS -----------------------------------------------------------------------------------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "http://localhost:3000" };

builder.Services.AddCors(options =>
    options.AddPolicy("frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

// ---- Swagger / OpenAPI ----------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TenantCore API",
        Version = "v1",
        Description = "Multi-tenant B2B project management API. Every request is automatically scoped " +
                      "to the tenant embedded in the bearer token."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token returned by /api/auth/login (no 'Bearer ' prefix needed)."
    });

    // Swashbuckle 10.x + Microsoft.OpenApi 2.x: the requirement is built from the document, and a
    // scheme is referenced via OpenApiSecuritySchemeReference.
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc, null)] = new List<string>()
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, "TenantCore.Api.xml");
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// ---- Apply migrations on startup (with a short retry for a still-booting database) ----------
await ApplyMigrationsAsync(app);

// ---- HTTP pipeline --------------------------------------------------------------------------
app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TenantCore API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("frontend");

app.UseAuthentication();
app.UseAuthorization();
app.UseTenantContext(); // after auth: reads tenant_id claim into the per-request ITenantContext

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!db.Database.IsRelational()) return; // tests use the in-memory provider

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied.");
            return;
        }
        catch (Exception ex) when (attempt < 10)
        {
            logger.LogWarning(ex, "Database not ready (attempt {Attempt}/10). Retrying in 3s...", attempt);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

// Exposed so the integration test project can reference the entry-point type via WebApplicationFactory.
public partial class Program { }
