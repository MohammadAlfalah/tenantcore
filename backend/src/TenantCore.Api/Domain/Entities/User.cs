using TenantCore.Api.Domain.Common;
using TenantCore.Api.Domain.Enums;

namespace TenantCore.Api.Domain.Entities;

/// <summary>
/// A member of a tenant. Email is globally unique across the whole system, so a person
/// belongs to exactly one tenant — this keeps login a simple email+password lookup with no
/// "which company?" prompt. Cross-tenant data access is still impossible because the issued
/// JWT is scoped to this user's <see cref="TenantId"/> and every query is filtered by it.
/// </summary>
public class User : ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    /// <summary>BCrypt hash of the password. Never the plaintext.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Member;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<ProjectTask> AssignedTasks { get; set; } = new List<ProjectTask>();
}
