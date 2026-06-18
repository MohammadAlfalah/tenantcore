using Microsoft.EntityFrameworkCore;
using TenantCore.Api.Common;
using TenantCore.Api.Domain.Entities;
using TenantCore.Api.Domain.Enums;
using TenantCore.Api.Infrastructure.Auth;
using TenantCore.Api.Infrastructure.Data;
using TenantCore.Api.Infrastructure.Tenancy;

namespace TenantCore.Api.Features.Members;

public interface IMemberService
{
    Task<IReadOnlyList<MemberDto>> ListAsync(CancellationToken ct);
    Task<MemberDto> CreateAsync(CreateMemberRequest request, CancellationToken ct);
    Task<MemberDto> UpdateAsync(Guid id, UpdateMemberRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public sealed class MemberService : IMemberService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITenantContext _tenant;

    public MemberService(AppDbContext db, IPasswordHasher hasher, ITenantContext tenant)
    {
        _db = db;
        _hasher = hasher;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<MemberDto>> ListAsync(CancellationToken ct) =>
        await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.FullName)
            .Select(u => new MemberDto(u.Id, u.FullName, u.Email, u.Role, u.CreatedAt))
            .ToListAsync(ct);

    public async Task<MemberDto> CreateAsync(CreateMemberRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // Email is globally unique — check across all tenants, hence IgnoreQueryFilters.
        var emailTaken = await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct);
        if (emailTaken)
            throw new ConflictException("A user with this email already exists.");

        var member = new User
        {
            // TenantId is auto-stamped from the ambient tenant context on SaveChanges.
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            Role = request.Role
        };

        _db.Users.Add(member);
        await _db.SaveChangesAsync(ct);

        return new MemberDto(member.Id, member.FullName, member.Email, member.Role, member.CreatedAt);
    }

    public async Task<MemberDto> UpdateAsync(Guid id, UpdateMemberRequest request, CancellationToken ct)
    {
        var member = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException("Member not found.");

        // Guardrail: never let the tenant lose its last Admin by demotion.
        if (member.Role == UserRole.Admin && request.Role != UserRole.Admin)
            await EnsureNotLastAdminAsync(member.Id, ct);

        member.FullName = request.FullName.Trim();
        member.Role = request.Role;
        await _db.SaveChangesAsync(ct);

        return new MemberDto(member.Id, member.FullName, member.Email, member.Role, member.CreatedAt);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var member = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException("Member not found.");

        // Guardrail: an Admin cannot remove their own account (avoids accidental self-lockout).
        if (member.Id == _tenant.UserId)
            throw new ValidationException("You cannot remove your own account.");

        // Guardrail: never delete the last Admin of a tenant.
        if (member.Role == UserRole.Admin)
            await EnsureNotLastAdminAsync(member.Id, ct);

        _db.Users.Remove(member);
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureNotLastAdminAsync(Guid excludingUserId, CancellationToken ct)
    {
        // Scoped query — counts admins of the current tenant only.
        var otherAdmins = await _db.Users
            .CountAsync(u => u.Role == UserRole.Admin && u.Id != excludingUserId, ct);

        if (otherAdmins == 0)
            throw new ConflictException("A tenant must always have at least one Admin.");
    }
}
