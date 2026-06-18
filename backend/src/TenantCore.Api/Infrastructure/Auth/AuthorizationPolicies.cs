using Microsoft.AspNetCore.Authorization;
using TenantCore.Api.Domain.Enums;

namespace TenantCore.Api.Infrastructure.Auth;

/// <summary>
/// Centralized role-based authorization policies. Roles are carried in the access token as the
/// standard role claim (the <see cref="UserRole"/> name), so these map directly onto it.
///
///   • <see cref="RequireAdmin"/>  — Admin only. Member management, destructive ops.
///   • <see cref="CanEdit"/>        — Admin or Member. Create/update/delete projects and tasks.
///   • (any authenticated user)     — Viewer included. Read access; expressed with [Authorize].
/// </summary>
public static class AuthorizationPolicies
{
    public const string RequireAdmin = "RequireAdmin";
    public const string CanEdit = "CanEdit";

    public static AuthorizationBuilder AddTenantCorePolicies(this AuthorizationBuilder builder) =>
        builder
            .AddPolicy(RequireAdmin, p => p.RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(CanEdit, p => p.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Member)));
}
