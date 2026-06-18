namespace TenantCore.Api.Domain.Common;

/// <summary>
/// Marker interface for every entity that belongs to a single tenant.
///
/// Any entity implementing this interface is:
///   1. Automatically filtered by the current tenant via an EF Core global query filter
///      (see <c>AppDbContext.OnModelCreating</c>), so cross-tenant reads are impossible.
///   2. Automatically stamped with the current <c>TenantId</c> on insert
///      (see <c>AppDbContext.SaveChangesAsync</c>), so a developer never has to set it by hand.
///
/// This is the single seam that makes the shared-database multi-tenancy model safe:
/// isolation is enforced by the data layer, not by remembering to add a WHERE clause.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
