using TenantCore.Api.Domain.Common;

namespace TenantCore.Api.Domain.Entities;

/// <summary>
/// A persisted refresh token. Access tokens are short-lived and stateless (JWT); refresh tokens
/// are long-lived, stored server-side, and rotated on every use so a stolen refresh token has a
/// limited blast radius. Tenant-scoped for completeness, though it is always looked up by its
/// random token value.
/// </summary>
public class RefreshToken : ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Cryptographically random, opaque string. The client stores this; the server stores it too.</summary>
    public string Token { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Set when the token is rotated or explicitly revoked (logout). A revoked token is unusable.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}
