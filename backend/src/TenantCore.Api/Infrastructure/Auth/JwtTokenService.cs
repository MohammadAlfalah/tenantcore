using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TenantCore.Api.Domain.Entities;

namespace TenantCore.Api.Infrastructure.Auth;

public interface IJwtTokenService
{
    /// <summary>Creates a signed access token embedding the user's id, tenant, and role.</summary>
    (string token, DateTimeOffset expiresAt) CreateAccessToken(User user, string tenantSlug);

    /// <summary>Creates an opaque, cryptographically-random refresh token value.</summary>
    string CreateRefreshTokenValue();
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public (string token, DateTimeOffset expiresAt) CreateAccessToken(User user, string tenantSlug)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, user.FullName),
            // ClaimTypes.Role drives [Authorize(Roles=...)] and policy checks.
            new(ClaimTypes.Role, user.Role.ToString()),
            new(TenantClaims.TenantId, user.TenantId.ToString()),
            new(TenantClaims.TenantSlug, tenantSlug),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expiresAt);
    }

    public string CreateRefreshTokenValue()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
}
