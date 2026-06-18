namespace TenantCore.Api.Infrastructure.Auth;

/// <summary>
/// Strongly-typed JWT configuration, bound from the "Jwt" section of configuration.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Secret signing key (HMAC-SHA256). MUST be overridden in production via env/secret.</summary>
    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "TenantCore";
    public string Audience { get; set; } = "TenantCore";

    /// <summary>Access token lifetime. Short by design — refresh tokens cover long sessions.</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh token lifetime in days.</summary>
    public int RefreshTokenDays { get; set; } = 7;
}
