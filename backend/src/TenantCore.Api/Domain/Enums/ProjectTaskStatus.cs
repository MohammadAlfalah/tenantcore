namespace TenantCore.Api.Domain.Enums;

/// <summary>
/// Lifecycle status of a task. Named <c>ProjectTaskStatus</c> rather than <c>TaskStatus</c>
/// to avoid colliding with <see cref="System.Threading.Tasks.TaskStatus"/>.
/// </summary>
public enum ProjectTaskStatus
{
    Todo = 0,
    InProgress = 1,
    InReview = 2,
    Done = 3
}
