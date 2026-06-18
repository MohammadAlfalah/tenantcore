namespace TenantCore.Api.Domain.Enums;

/// <summary>
/// Role of a user <b>within their tenant</b>. Roles do not cross tenant boundaries —
/// being an Admin of tenant A grants nothing in tenant B.
/// </summary>
public enum UserRole
{
    /// <summary>Read-only. Can view projects and tasks but cannot modify anything.</summary>
    Viewer = 0,

    /// <summary>Can create and manage projects and tasks, but cannot manage members.</summary>
    Member = 1,

    /// <summary>Full control of the tenant: projects, tasks, and member management.</summary>
    Admin = 2
}
