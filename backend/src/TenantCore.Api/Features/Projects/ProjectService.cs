using Microsoft.EntityFrameworkCore;
using TenantCore.Api.Common;
using TenantCore.Api.Domain.Entities;
using TenantCore.Api.Domain.Enums;
using TenantCore.Api.Features.Tasks;
using TenantCore.Api.Infrastructure.Data;
using TenantCore.Api.Infrastructure.Tenancy;

namespace TenantCore.Api.Features.Projects;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectSummaryDto>> ListAsync(CancellationToken ct);
    Task<ProjectDetailDto> GetAsync(Guid id, CancellationToken ct);
    Task<ProjectDetailDto> CreateAsync(CreateProjectRequest request, CancellationToken ct);
    Task<ProjectDetailDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public sealed class ProjectService : IProjectService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public ProjectService(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<ProjectSummaryDto>> ListAsync(CancellationToken ct)
    {
        // No tenant WHERE clause needed — the global query filter scopes this automatically.
        return await _db.Projects
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProjectSummaryDto(
                p.Id,
                p.Name,
                p.Description,
                p.CreatedBy != null ? p.CreatedBy.FullName : "Unknown",
                p.CreatedAt,
                p.Tasks.Count,
                p.Tasks.Count(t => t.Status != ProjectTaskStatus.Done)))
            .ToListAsync(ct);
    }

    public async Task<ProjectDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .Include(p => p.CreatedBy)
            .Include(p => p.Tasks).ThenInclude(t => t.Assignee)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project not found.");

        var tasks = project.Tasks
            .OrderBy(t => t.Status)
            .ThenByDescending(t => t.CreatedAt)
            .Select(TaskMapping.ToDto)
            .ToList();

        return new ProjectDetailDto(
            project.Id, project.Name, project.Description,
            project.CreatedBy?.FullName ?? "Unknown", project.CreatedAt, tasks);
    }

    public async Task<ProjectDetailDto> CreateAsync(CreateProjectRequest request, CancellationToken ct)
    {
        var project = new Project
        {
            // TenantId is auto-stamped on SaveChanges from the ambient tenant context.
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            CreatedByUserId = _tenant.UserId!.Value
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(project.Id, ct);
    }

    public async Task<ProjectDetailDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project not found.");

        project.Name = request.Name.Trim();
        project.Description = request.Description?.Trim();
        await _db.SaveChangesAsync(ct);

        return await GetAsync(project.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project not found.");

        _db.Projects.Remove(project); // cascade removes the project's tasks
        await _db.SaveChangesAsync(ct);
    }
}
