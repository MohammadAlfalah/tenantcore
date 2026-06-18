using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TenantCore.Api.Infrastructure.Auth;

namespace TenantCore.Api.Infrastructure.Tenancy;

/// <summary>
/// Runs after authentication. If the request carries a valid, authenticated principal, it copies the
/// tenant_id and sub claims into the per-request <see cref="ITenantContext"/>. From that point on every
/// database query the request makes is automatically scoped to that tenant.
///
/// Unauthenticated requests (register, login, refresh, swagger, health) simply pass through with no
/// tenant set — and because the query filters match zero rows without a tenant, they cannot read data.
/// </summary>
public sealed class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var tenantId = GetClaim(user, TenantClaims.TenantId);
            var userId = GetClaim(user, JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier);

            if (Guid.TryParse(tenantId, out var t) && Guid.TryParse(userId, out var u))
            {
                tenantContext.SetContext(t, u);
            }
            else
            {
                // Authenticated but missing/garbled tenant claims => reject rather than run unscoped.
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Token is missing valid tenant claims." });
                return;
            }
        }

        await _next(context);
    }

    private static string? GetClaim(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (var type in claimTypes)
        {
            var value = user.FindFirstValue(type);
            if (!string.IsNullOrEmpty(value)) return value;
        }
        return null;
    }
}

public static class TenantMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantMiddleware>();
}
