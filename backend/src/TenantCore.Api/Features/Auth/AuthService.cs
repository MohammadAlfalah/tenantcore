using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TenantCore.Api.Common;
using TenantCore.Api.Domain.Entities;
using TenantCore.Api.Domain.Enums;
using TenantCore.Api.Infrastructure.Auth;
using TenantCore.Api.Infrastructure.Data;
using TenantCore.Api.Infrastructure.Tenancy;

namespace TenantCore.Api.Features.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct);
    Task LogoutAsync(RefreshRequest request, CancellationToken ct);
    Task<UserDto> GetCurrentUserAsync(CancellationToken ct);
}

public sealed partial class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly ITenantContext _tenant;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        AppDbContext db,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        ITenantContext tenant,
        Microsoft.Extensions.Options.IOptions<JwtOptions> jwtOptions)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
        _tenant = tenant;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // IgnoreQueryFilters: no tenant in scope yet, so the global filter would otherwise hide all users.
        var emailTaken = await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct);
        if (emailTaken)
            throw new ConflictException("An account with this email already exists.");

        var tenant = new Tenant
        {
            Name = request.TenantName.Trim(),
            Slug = await GenerateUniqueSlugAsync(request.TenantName, ct)
        };

        var user = new User
        {
            TenantId = tenant.Id,            // set explicitly — there is no ambient tenant to stamp it
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            Role = UserRole.Admin            // the founder of a tenant is its first Admin
        };

        _db.Tenants.Add(tenant);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, tenant, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAppException("Invalid email or password.");

        return await IssueTokensAsync(user, user.Tenant, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var token = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Include(r => r.User).ThenInclude(u => u.Tenant)
            .FirstOrDefaultAsync(r => r.Token == request.RefreshToken, ct);

        if (token is null || !token.IsActive)
            throw new UnauthorizedAppException("Refresh token is invalid or expired.");

        // Rotate: the old token is single-use. Reusing it (e.g. a stolen copy) will now fail.
        token.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await IssueTokensAsync(token.User, token.User.Tenant, ct);
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken ct)
    {
        var token = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Token == request.RefreshToken, ct);

        if (token is { RevokedAt: null })
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<UserDto> GetCurrentUserAsync(CancellationToken ct)
    {
        var userId = _tenant.UserId
            ?? throw new UnauthorizedAppException("Not authenticated.");

        // Scoped query — the filter guarantees we can only ever load our own tenant's user.
        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User not found.");

        return ToDto(user, user.Tenant);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, Tenant tenant, CancellationToken ct)
    {
        var (accessToken, expiresAt) = _jwt.CreateAccessToken(user, tenant.Slug);

        var refreshToken = new RefreshToken
        {
            TenantId = user.TenantId,        // explicit — issued on an anonymous endpoint
            UserId = user.Id,
            Token = _jwt.CreateRefreshTokenValue(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenDays)
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, refreshToken.Token, expiresAt, ToDto(user, tenant));
    }

    private static UserDto ToDto(User user, Tenant tenant) =>
        new(user.Id, user.Email, user.FullName, user.Role, tenant.Id, tenant.Name, tenant.Slug);

    private async Task<string> GenerateUniqueSlugAsync(string tenantName, CancellationToken ct)
    {
        var baseSlug = Slugify(tenantName);
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "tenant";

        var slug = baseSlug;
        var suffix = 1;
        while (await _db.Tenants.AnyAsync(t => t.Slug == slug, ct))
            slug = $"{baseSlug}-{++suffix}";

        return slug;
    }

    private static string Slugify(string value)
    {
        value = value.Trim().ToLowerInvariant();
        value = NonAlphanumeric().Replace(value, "-");
        value = MultiDash().Replace(value, "-").Trim('-');
        return value.Length > 100 ? value[..100].Trim('-') : value;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultiDash();
}
