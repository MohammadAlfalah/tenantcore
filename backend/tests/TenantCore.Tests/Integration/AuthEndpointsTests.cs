using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TenantCore.Api.Domain.Enums;
using TenantCore.Api.Features.Auth;

namespace TenantCore.Tests.Integration;

public class AuthEndpointsTests : IClassFixture<TenantCoreWebAppFactory>
{
    private readonly TenantCoreWebAppFactory _factory;

    public AuthEndpointsTests(TenantCoreWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_creates_a_tenant_and_an_admin_user()
    {
        var client = _factory.CreateClient();

        var auth = await TestApi.RegisterTenantAsync(client, "Acme Inc", "founder@acme.com");

        auth.AccessToken.Should().NotBeNullOrEmpty();
        auth.RefreshToken.Should().NotBeNullOrEmpty();
        auth.User.Role.Should().Be(UserRole.Admin);
        auth.User.TenantName.Should().Be("Acme Inc");
        auth.User.TenantSlug.Should().Be("acme-inc");
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_409()
    {
        var client = _factory.CreateClient();
        await TestApi.RegisterTenantAsync(client, "First Co", "dupe@x.com");

        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Second Co", "Someone", "dupe@x.com", "password123"));

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_with_correct_password_succeeds_and_wrong_password_is_401()
    {
        var client = _factory.CreateClient();
        await TestApi.RegisterTenantAsync(client, "Login Co", "login@x.com", "correct-horse");

        var ok = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("login@x.com", "correct-horse"));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);

        var bad = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("login@x.com", "wrong"));
        bad.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_the_old_one_stops_working()
    {
        var client = _factory.CreateClient();
        var auth = await TestApi.RegisterTenantAsync(client, "Refresh Co", "refresh@x.com");

        var first = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // The original refresh token was rotated => reusing it must now fail.
        var replay = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_requires_authentication_and_returns_the_current_user()
    {
        var client = _factory.CreateClient();

        var anon = await client.GetAsync("/api/auth/me");
        anon.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var auth = await TestApi.RegisterTenantAsync(client, "Me Co", "me@x.com");
        var me = await client.WithToken(auth.AccessToken).GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await me.ReadAsync<UserDto>();
        dto!.Email.Should().Be("me@x.com");
    }
}
