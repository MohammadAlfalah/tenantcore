namespace TenantCore.Api.Infrastructure.Tenancy;

/// <summary>
/// Default mutable implementation of <see cref="ITenantContext"/>. One instance per request.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }

    public bool HasTenant => TenantId.HasValue;

    public Guid RequireTenantId() =>
        TenantId ?? throw new InvalidOperationException(
            "No tenant in scope. This operation requires an authenticated, tenant-scoped request.");

    public void SetContext(Guid tenantId, Guid userId)
    {
        TenantId = tenantId;
        UserId = userId;
    }
}
