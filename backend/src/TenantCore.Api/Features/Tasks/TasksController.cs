using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantCore.Api.Infrastructure.Auth;

namespace TenantCore.Api.Features.Tasks;

[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class TasksController : ControllerBase
{
    private readonly ITaskService _tasks;

    public TasksController(ITaskService tasks) => _tasks = tasks;

    /// <summary>List the tasks of a project.</summary>
    [HttpGet("api/projects/{projectId:guid}/tasks")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> ListForProject(Guid projectId, CancellationToken ct)
        => Ok(await _tasks.ListForProjectAsync(projectId, ct));

    /// <summary>Create a task in a project. Admin or Member.</summary>
    [HttpPost("api/projects/{projectId:guid}/tasks")]
    [Authorize(Policy = AuthorizationPolicies.CanEdit)]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskDto>> Create(Guid projectId, CreateTaskRequest request, CancellationToken ct)
    {
        var created = await _tasks.CreateAsync(projectId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Get a single task.</summary>
    [HttpGet("api/tasks/{id:guid}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _tasks.GetAsync(id, ct));

    /// <summary>Update a task's fields. Admin or Member.</summary>
    [HttpPut("api/tasks/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanEdit)]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> Update(Guid id, UpdateTaskRequest request, CancellationToken ct)
        => Ok(await _tasks.UpdateAsync(id, request, ct));

    /// <summary>Change just a task's status (e.g. dragging across a board). Admin or Member.</summary>
    [HttpPatch("api/tasks/{id:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.CanEdit)]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> UpdateStatus(Guid id, UpdateTaskStatusRequest request, CancellationToken ct)
        => Ok(await _tasks.UpdateStatusAsync(id, request, ct));

    /// <summary>Delete a task. Admin or Member.</summary>
    [HttpDelete("api/tasks/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.CanEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _tasks.DeleteAsync(id, ct);
        return NoContent();
    }
}
