using TenantCore.Api.Domain.Common;

namespace TenantCore.Api.Domain.Entities;

/// <summary>
/// A project owned by a tenant. Contains tasks. Tenant-scoped: it can only ever be seen or
/// modified by users of the same tenant.
/// </summary>
public class Project : ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
}
