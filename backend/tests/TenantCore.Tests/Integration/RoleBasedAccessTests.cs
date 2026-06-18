using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TenantCore.Api.Domain.Enums;
using TenantCore.Api.Features.Auth;
using TenantCore.Api.Features.Members;
using TenantCore.Api.Features.Projects;
using TenantCore.Api.Features.Tasks;

namespace TenantCore.Tests.Integration;

/// <summary>
/// Verifies the three-tier RBAC: Viewer (read-only), Member (manage tasks), Admin (everything).
/// </summary>
public class RoleBasedAccessTests : IClassFixture<TenantCoreWebAppFactory>
{
    private readonly TenantCoreWebAppFactory _factory;

    public RoleBasedAccessTests(TenantCoreWebAppFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginClientAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await resp.ReadAsync<AuthResponse>();
        return client.WithToken(auth!.AccessToken);
    }

    [Fact]
    public async Task Roles_grant_the_expected_capabilities()
    {
        // --- Admin sets up the tenant, a project, and two more members. ---
        var admin = _factory.CreateClient();
        var adminAuth = await TestApi.RegisterTenantAsync(admin, "RBAC Co", "admin@rbac.com", "password123");
        admin.WithToken(adminAuth.AccessToken);

        await admin.PostAsJsonAsync("/api/members",
            new CreateMemberRequest("Mem Ber", "member@rbac.com", "password123", UserRole.Member), TestApi.Json);
        await admin.PostAsJsonAsync("/api/members",
            new CreateMemberRequest("View Er", "viewer@rbac.com", "password123", UserRole.Viewer), TestApi.Json);

        var project = await (await admin.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest("Shared Project", null))).ReadAsync<ProjectDetailDto>();

        // --- Member: can manage tasks, cannot manage projects or members. ---
        var member = await LoginClientAsync("member@rbac.com", "password123");

        (await member.GetAsync("/api/projects")).StatusCode.Should().Be(HttpStatusCode.OK);

        var memberCreatesProject = await member.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest("Nope", null));
        memberCreatesProject.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var memberCreatesTask = await member.PostAsJsonAsync($"/api/projects/{project!.Id}/tasks",
            new CreateTaskRequest("Member's task", null, null, ProjectTaskStatus.Todo), TestApi.Json);
        memberCreatesTask.StatusCode.Should().Be(HttpStatusCode.Created);

        var memberManagesMembers = await member.PostAsJsonAsync("/api/members",
            new CreateMemberRequest("X", "x@rbac.com", "password123", UserRole.Viewer), TestApi.Json);
        memberManagesMembers.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // --- Viewer: read-only. ---
        var viewer = await LoginClientAsync("viewer@rbac.com", "password123");

        (await viewer.GetAsync("/api/projects")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.GetAsync($"/api/projects/{project.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var viewerCreatesTask = await viewer.PostAsJsonAsync($"/api/projects/{project.Id}/tasks",
            new CreateTaskRequest("Should fail", null, null, ProjectTaskStatus.Todo), TestApi.Json);
        viewerCreatesTask.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var viewerCreatesProject = await viewer.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest("Should fail", null));
        viewerCreatesProject.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
