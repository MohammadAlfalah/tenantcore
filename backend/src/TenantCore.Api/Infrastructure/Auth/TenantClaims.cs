namespace TenantCore.Api.Infrastructure.Auth;

/// <summary>
/// Custom JWT claim type names used to carry tenant identity inside the access token.
/// The standard claims (sub, email, role, name) cover the rest.
/// </summary>
public static class TenantClaims
{
    /// <summary>The tenant the user belongs to. Read by the tenant middleware to scope all data.</summary>
    public const string TenantId = "tenant_id";

    /// <summary>The tenant's slug, included for convenience on the client.</summary>
    public const string TenantSlug = "tenant_slug";
}
