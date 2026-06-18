using System.ComponentModel.DataAnnotations;
using TenantCore.Api.Domain.Enums;

namespace TenantCore.Api.Features.Tasks;

public record CreateTaskRequest(
    [Required, StringLength(300, MinimumLength = 1)] string Title,
    [StringLength(4000)] string? Description,
    Guid? AssigneeUserId,
    ProjectTaskStatus Status = ProjectTaskStatus.Todo);

public record UpdateTaskRequest(
    [Required, StringLength(300, MinimumLength = 1)] string Title,
    [StringLength(4000)] string? Description,
    Guid? AssigneeUserId,
    ProjectTaskStatus Status);

public record UpdateTaskStatusRequest(
    [Required] ProjectTaskStatus Status);

public record TaskDto(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    ProjectTaskStatus Status,
    Guid? AssigneeUserId,
    string? AssigneeName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
