using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TenantCore.Api.Domain.Entities;
using TenantCore.Api.Infrastructure.Tenancy;

namespace TenantCore.Tests.Unit;

/// <summary>
/// These are the load-bearing tests of the whole system: they prove that the EF Core global query
/// filter and the SaveChanges tenant stamping make cross-tenant reads and writes impossible.
/// </summary>
public class TenantIsolationTests
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    [Fact]
    public async Task Query_returns_only_current_tenants_rows()
    {
        var factory = TestDb.NewStore();

        // Tenant A creates a project.
        await using (var db = factory(TestDb.TenantFor(_tenantA)))
        {
            db.Projects.Add(new Project { Name = "A's secret project", CreatedByUserId = Guid.NewGuid() });
            await db.SaveChangesAsync();
        }

        // Tenant B must not see it.
        await using (var db = factory(TestDb.TenantFor(_tenantB)))
        {
            var visibleToB = await db.Projects.ToListAsync();
            visibleToB.Should().BeEmpty("tenant B must never see tenant A's data");
        }

        // Tenant A still sees its own.
        await using (var db = factory(TestDb.TenantFor(_tenantA)))
        {
            var visibleToA = await db.Projects.ToListAsync();
            visibleToA.Should().ContainSingle().Which.Name.Should().Be("A's secret project");
        }
    }

    [Fact]
    public async Task Inserted_entity_is_auto_stamped_with_current_tenant()
    {
        var factory = TestDb.NewStore();

        Guid projectId;
        await using (var db = factory(TestDb.TenantFor(_tenantA)))
        {
            var project = new Project { Name = "Stamp me", CreatedByUserId = Guid.NewGuid() };
            db.Projects.Add(project);          // note: we never set TenantId
            await db.SaveChangesAsync();
            projectId = project.Id;
            project.TenantId.Should().Be(_tenantA, "SaveChanges should stamp the ambient tenant");
        }

        // Confirm it really persisted under tenant A by reading it back with IgnoreQueryFilters.
        await using (var db = factory(TestDb.TenantFor(_tenantA)))
        {
            var raw = await db.Projects.IgnoreQueryFilters().SingleAsync(p => p.Id == projectId);
            raw.TenantId.Should().Be(_tenantA);
        }
    }

    [Fact]
    public async Task Writing_a_row_for_another_tenant_is_blocked()
    {
        var factory = TestDb.NewStore();

        await using var db = factory(TestDb.TenantFor(_tenantA));
        // Deliberately try to plant a row belonging to tenant B while scoped to tenant A.
        db.Projects.Add(new Project { TenantId = _tenantB, Name = "Trojan", CreatedByUserId = Guid.NewGuid() });

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cross-tenant write blocked*");
    }

    [Fact]
    public async Task No_tenant_in_scope_sees_no_rows()
    {
        var factory = TestDb.NewStore();

        await using (var db = factory(TestDb.TenantFor(_tenantA)))
        {
            db.Projects.Add(new Project { Name = "A's project", CreatedByUserId = Guid.NewGuid() });
            await db.SaveChangesAsync();
        }

        // An empty tenant context (e.g. an anonymous request) must read zero rows — fail closed.
        await using (var db = factory(new TenantContext()))
        {
            (await db.Projects.ToListAsync()).Should().BeEmpty();
        }
    }
}
