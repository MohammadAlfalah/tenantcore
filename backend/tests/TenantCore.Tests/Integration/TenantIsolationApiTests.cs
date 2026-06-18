using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TenantCore.Api.Features.Projects;

namespace TenantCore.Tests.Integration;

/// <summary>
/// End-to-end proof, over real HTTP, that two tenants cannot see or touch each other's data even
/// though they hit the same API and the same database.
/// </summary>
public class TenantIsolationApiTests : IClassFixture<TenantCoreWebAppFactory>
{
    private readonly TenantCoreWebAppFactory _factory;

    public TenantIsolationApiTests(TenantCoreWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task A_tenant_cannot_list_or_fetch_another_tenants_project()
    {
        // Tenant A creates a project.
        var clientA = _factory.CreateClient();
        var authA = await TestApi.RegisterTenantAsync(clientA, "Tenant A", "a@a.com");
        clientA.WithToken(authA.AccessToken);

        var created = await (await clientA.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest("A's Roadmap", "secret"))).ReadAsync<ProjectDetailDto>();
        created!.Name.Should().Be("A's Roadmap");

        // Tenant B registers separately.
        var clientB = _factory.CreateClient();
        var authB = await TestApi.RegisterTenantAsync(clientB, "Tenant B", "b@b.com");
        clientB.WithToken(authB.AccessToken);

        // B's project list is empty...
        var bList = await (await clientB.GetAsync("/api/projects")).ReadAsync<List<ProjectSummaryDto>>();
        bList.Should().BeEmpty();

        // ...and B cannot fetch A's project by id (the global filter makes it "not found").
        var bFetch = await clientB.GetAsync($"/api/projects/{created.Id}");
        bFetch.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // ...and B cannot delete A's project.
        var bDelete = await clientB.DeleteAsync($"/api/projects/{created.Id}");
        bDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // A still sees exactly its one project.
        var aList = await (await clientA.GetAsync("/api/projects")).ReadAsync<List<ProjectSummaryDto>>();
        aList.Should().ContainSingle();
    }

    [Fact]
    public async Task Unauthenticated_requests_are_rejected()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/projects");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
