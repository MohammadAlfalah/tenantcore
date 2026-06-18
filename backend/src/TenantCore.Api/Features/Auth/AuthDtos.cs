using System.ComponentModel.DataAnnotations;
using TenantCore.Api.Domain.Enums;

namespace TenantCore.Api.Features.Auth;

/// <summary>Tenant signup: creates a brand-new isolated tenant and its first Admin user.</summary>
public record RegisterRequest(
    [Required, StringLength(200, MinimumLength = 2)] string TenantName,
    [Required, StringLength(200, MinimumLength = 2)] string FullName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 8)] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record RefreshRequest(
    [Required] string RefreshToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    UserDto User);

/// <summary>Public shape of a user — never includes the password hash.</summary>
public record UserDto(
    Guid Id,
    string Email,
    string FullName,
    UserRole Role,
    Guid TenantId,
    string TenantName,
    string TenantSlug);
