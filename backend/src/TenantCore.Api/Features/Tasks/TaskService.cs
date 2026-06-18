using Microsoft.EntityFrameworkCore;
using TenantCore.Api.Common;
using TenantCore.Api.Domain.Entities;
using TenantCore.Api.Infrastructure.Data;

namespace TenantCore.Api.Features.Tasks;

public interface ITaskService
{
    Task<IReadOnlyList<TaskDto>> ListForProjectAsync(Guid projectId, CancellationToken ct);
    Task<TaskDto> GetAsync(Guid id, CancellationToken ct);
    Task<TaskDto> CreateAsync(Guid projectId, CreateTaskRequest request, CancellationToken ct);
    Task<TaskDto> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken ct);
    Task<TaskDto> UpdateStatusAsync(Guid id, UpdateTaskStatusRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public sealed class TaskService : ITaskService
{
    private readonly AppDbContext _db;

    public TaskService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<TaskDto>> ListForProjectAsync(Guid projectId, CancellationToken ct)
    {
        await EnsureProjectExistsAsync(projectId, ct);

        return await _db.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.Status)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new TaskDto(
                t.Id, t.ProjectId, t.Title, t.Description, t.Status,
                t.AssigneeUserId, t.Assignee != null ? t.Assignee.FullName : null,
                t.CreatedAt, t.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<TaskDto> GetAsync(Guid id, CancellationToken ct)
    {
        var task = await _db.Tasks
            .AsNoTracking()
            .Include(t => t.Assignee)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Task not found.");

        return TaskMapping.ToDto(task);
    }

    public async Task<TaskDto> CreateAsync(Guid projectId, CreateTaskRequest request, CancellationToken ct)
    {
        await EnsureProjectExistsAsync(projectId, ct);
        await ValidateAssigneeAsync(request.AssigneeUserId, ct);

        var task = new ProjectTask
        {
            ProjectId = projectId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Status = request.Status,
            AssigneeUserId = request.AssigneeUserId
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(task.Id, ct);
    }

    public async Task<TaskDto> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken ct)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Task not found.");

        await ValidateAssigneeAsync(request.AssigneeUserId, ct);

        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.Status = request.Status;
        task.AssigneeUserId = request.AssigneeUserId;
        await _db.SaveChangesAsync(ct);

        return await GetAsync(task.Id, ct);
    }

    public async Task<TaskDto> UpdateStatusAsync(Guid id, UpdateTaskStatusRequest request, CancellationToken ct)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Task not found.");

        task.Status = request.Status;
        await _db.SaveChangesAsync(ct);

        return await GetAsync(task.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Task not found.");

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureProjectExistsAsync(Guid projectId, CancellationToken ct)
    {
        var exists = await _db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!exists) throw new NotFoundException("Project not found.");
    }

    /// <summary>
    /// Ensures an assignee, if provided, is a member of the SAME tenant. The global query filter
    /// already restricts Users to the current tenant, so a foreign user id simply won't be found.
    /// </summary>
    private async Task ValidateAssigneeAsync(Guid? assigneeUserId, CancellationToken ct)
    {
        if (assigneeUserId is null) return;

        var exists = await _db.Users.AnyAsync(u => u.Id == assigneeUserId.Value, ct);
        if (!exists) throw new ValidationException("Assignee must be a member of your organization.");
    }
}
