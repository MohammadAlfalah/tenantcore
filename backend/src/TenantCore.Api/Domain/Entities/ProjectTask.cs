using TenantCore.Api.Domain.Common;
using TenantCore.Api.Domain.Enums;

namespace TenantCore.Api.Domain.Entities;

/// <summary>
/// A unit of work inside a project. Optionally assigned to a tenant member. Tenant-scoped.
/// </summary>
public class ProjectTask : ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.Todo;

    /// <summary>The member this task is assigned to, if any. Must belong to the same tenant.</summary>
    public Guid? AssigneeUserId { get; set; }
    public User? Assignee { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
