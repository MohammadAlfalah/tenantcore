using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantCore.Api.Infrastructure.Auth;

namespace TenantCore.Api.Features.Projects;

[ApiController]
[Route("api/projects")]
[Authorize] // any authenticated tenant member (incl. Viewer) may read
[Produces("application/json")]
public sealed class ProjectsController : ControllerBase
{
    private readonly IProjectService _projects;

    public ProjectsController(IProjectService projects) => _projects = projects;

    /// <summary>List all projects in the caller's tenant.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProjectSummaryDto>>> List(CancellationToken ct)
        => Ok(await _projects.ListAsync(ct));

    /// <summary>Get a single project with its tasks.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProjectDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDetailDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _projects.GetAsync(id, ct));

    /// <summary>Create a project. Admin only.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [ProducesResponseType(typeof(ProjectDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProjectDetailDto>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var created = await _projects.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Update a project. Admin only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [ProducesResponseType(typeof(ProjectDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectDetailDto>> Update(Guid id, UpdateProjectRequest request, CancellationToken ct)
        => Ok(await _projects.UpdateAsync(id, request, ct));

    /// <summary>Delete a project and its tasks. Admin only.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _projects.DeleteAsync(id, ct);
        return NoContent();
    }
}
