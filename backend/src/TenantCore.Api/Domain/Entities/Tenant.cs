namespace TenantCore.Api.Domain.Entities;

/// <summary>
/// A tenant is an isolated company/organization. It is the root of an isolation boundary:
/// every <see cref="User"/>, <see cref="Project"/> and <see cref="ProjectTask"/> belongs to
/// exactly one tenant. The Tenant entity itself is NOT tenant-scoped (it has no TenantId) —
/// it is the thing being scoped to.
/// </summary>
public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable company name, e.g. "Acme Inc".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-safe unique identifier, e.g. "acme-inc". Used for display and lookups.</summary>
    public string Slug { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
