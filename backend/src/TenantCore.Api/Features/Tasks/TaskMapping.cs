using TenantCore.Api.Domain.Entities;

namespace TenantCore.Api.Features.Tasks;

/// <summary>Maps a <see cref="ProjectTask"/> entity to its public <see cref="TaskDto"/>.</summary>
public static class TaskMapping
{
    public static TaskDto ToDto(ProjectTask t) => new(
        t.Id,
        t.ProjectId,
        t.Title,
        t.Description,
        t.Status,
        t.AssigneeUserId,
        t.Assignee?.FullName,
        t.CreatedAt,
        t.UpdatedAt);
}
