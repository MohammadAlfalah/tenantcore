namespace TenantCore.Api.Infrastructure.Tenancy;

/// <summary>
/// Ambient, per-request information about who is making the call. Registered as a SCOPED service
/// so each HTTP request gets its own instance. <see cref="TenantMiddleware"/> populates it from the
/// authenticated user's JWT claims, and <c>AppDbContext</c> reads it to scope every query.
/// </summary>
public interface ITenantContext
{
    /// <summary>The current tenant, or null on unauthenticated/auth endpoints (register, login, refresh).</summary>
    Guid? TenantId { get; }

    /// <summary>The current user id, or null if unauthenticated.</summary>
    Guid? UserId { get; }

    bool HasTenant { get; }

    /// <summary>Throws if there is no tenant in scope. Use when a tenant is required to proceed.</summary>
    Guid RequireTenantId();

    /// <summary>Set the ambient identity for the current request. Called once by the middleware.</summary>
    void SetContext(Guid tenantId, Guid userId);
}
