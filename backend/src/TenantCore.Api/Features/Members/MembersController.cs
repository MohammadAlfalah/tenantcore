using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantCore.Api.Infrastructure.Auth;

namespace TenantCore.Api.Features.Members;

[ApiController]
[Route("api/members")]
[Authorize]
[Produces("application/json")]
public sealed class MembersController : ControllerBase
{
    private readonly IMemberService _members;

    public MembersController(IMemberService members) => _members = members;

    /// <summary>List the members of the caller's tenant. Any authenticated member (used for assignee pickers).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MemberDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> List(CancellationToken ct)
        => Ok(await _members.ListAsync(ct));

    /// <summary>Invite/create a new member. Admin only.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MemberDto>> Create(CreateMemberRequest request, CancellationToken ct)
    {
        var created = await _members.CreateAsync(request, ct);
        return CreatedAtAction(nameof(List), new { id = created.Id }, created);
    }

    /// <summary>Update a member's name and role. Admin only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MemberDto>> Update(Guid id, UpdateMemberRequest request, CancellationToken ct)
        => Ok(await _members.UpdateAsync(id, request, ct));

    /// <summary>Remove a member. Admin only.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _members.DeleteAsync(id, ct);
        return NoContent();
    }
}
