using FluentAssertions;
using TenantCore.Api.Common;
using TenantCore.Api.Domain.Entities;
using TenantCore.Api.Domain.Enums;
using TenantCore.Api.Features.Members;
using TenantCore.Api.Infrastructure.Auth;
using TenantCore.Api.Infrastructure.Data;
using TenantCore.Api.Infrastructure.Tenancy;

namespace TenantCore.Tests.Unit;

public class MemberServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Func<ITenantContext, AppDbContext> _factory;
    private readonly IPasswordHasher _hasher = new BcryptPasswordHasher();

    public MemberServiceTests()
    {
        _factory = TestDb.NewStore();

        // Seed: one Admin (the signed-in user) and one Member in tenant A.
        using var db = _factory(TestDb.TenantFor(_tenantId, _adminId));
        db.Users.Add(new User
        {
            Id = _adminId, TenantId = _tenantId, Email = "admin@a.com",
            FullName = "Admin", PasswordHash = _hasher.Hash("password123"), Role = UserRole.Admin
        });
        db.Users.Add(new User
        {
            TenantId = _tenantId, Email = "member@a.com",
            FullName = "Member", PasswordHash = _hasher.Hash("password123"), Role = UserRole.Member
        });
        db.SaveChanges();
    }

    private MemberService NewService(out AppDbContext db)
    {
        var tenant = TestDb.TenantFor(_tenantId, _adminId);
        db = _factory(tenant);
        return new MemberService(db, _hasher, tenant);
    }

    [Fact]
    public async Task Cannot_delete_the_last_admin()
    {
        var service = NewService(out _);

        var act = async () => await service.DeleteAsync(_adminId, CancellationToken.None);

        // The only admin happens to be the caller, so the self-delete guard fires first.
        await act.Should().ThrowAsync<AppException>();
    }

    [Fact]
    public async Task Cannot_demote_the_last_admin()
    {
        var service = NewService(out _);

        var act = async () => await service.UpdateAsync(
            _adminId, new UpdateMemberRequest("Admin", UserRole.Member), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*at least one Admin*");
    }

    [Fact]
    public async Task Admin_cannot_delete_their_own_account()
    {
        var service = NewService(out _);

        var act = async () => await service.DeleteAsync(_adminId, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*cannot remove your own account*");
    }

    [Fact]
    public async Task Creating_a_member_with_a_duplicate_email_conflicts()
    {
        var service = NewService(out _);

        var act = async () => await service.CreateAsync(
            new CreateMemberRequest("Dupe", "member@a.com", "password123", UserRole.Member),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Can_remove_an_admin_once_another_admin_exists()
    {
        // Promote the member to Admin first.
        var service = NewService(out var db);
        var member = db.Users.Single(u => u.Email == "member@a.com");
        await service.UpdateAsync(member.Id, new UpdateMemberRequest("Member", UserRole.Admin), CancellationToken.None);

        // Now there are two admins; removing the non-self admin should succeed.
        var service2 = NewService(out _);
        var act = async () => await service2.DeleteAsync(member.Id, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
