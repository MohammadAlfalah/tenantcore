using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TenantCore.Api.Features.Auth;

namespace TenantCore.Tests.Integration;

/// <summary>Convenience helpers for driving the API over HTTP in integration tests.</summary>
public static class TestApi
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Signs up a brand-new tenant and returns its auth response (the founder is an Admin).</summary>
    public static async Task<AuthResponse> RegisterTenantAsync(
        HttpClient client, string tenantName, string email, string password = "password123", string? fullName = null)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(tenantName, fullName ?? "Owner", email, password));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
    }

    /// <summary>Returns a client whose Authorization header carries the given access token.</summary>
    public static HttpClient WithToken(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static async Task<T?> ReadAsync<T>(this HttpResponseMessage resp)
    {
        var body = await resp.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
            throw new Xunit.Sdk.XunitException(
                $"Expected a JSON body but got HTTP {(int)resp.StatusCode} {resp.StatusCode}. " +
                $"WWW-Authenticate: {string.Join(" | ", resp.Headers.WwwAuthenticate)}");
        return JsonSerializer.Deserialize<T>(body, Json);
    }
}
