using System.ComponentModel.DataAnnotations;
using TenantCore.Api.Features.Tasks;

namespace TenantCore.Api.Features.Projects;

public record CreateProjectRequest(
    [Required, StringLength(200, MinimumLength = 2)] string Name,
    [StringLength(2000)] string? Description);

public record UpdateProjectRequest(
    [Required, StringLength(200, MinimumLength = 2)] string Name,
    [StringLength(2000)] string? Description);

/// <summary>Summary used in list views — includes a task count but not the tasks themselves.</summary>
public record ProjectSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    int TaskCount,
    int OpenTaskCount);

/// <summary>Full project detail including its tasks.</summary>
public record ProjectDetailDto(
    Guid Id,
    string Name,
    string? Description,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<TaskDto> Tasks);
