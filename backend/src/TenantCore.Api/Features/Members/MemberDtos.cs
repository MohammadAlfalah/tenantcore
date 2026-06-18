using System.ComponentModel.DataAnnotations;
using TenantCore.Api.Domain.Enums;

namespace TenantCore.Api.Features.Members;

public record CreateMemberRequest(
    [Required, StringLength(200, MinimumLength = 2)] string FullName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 8)] string Password,
    [Required] UserRole Role);

public record UpdateMemberRequest(
    [Required, StringLength(200, MinimumLength = 2)] string FullName,
    [Required] UserRole Role);

public record MemberDto(
    Guid Id,
    string FullName,
    string Email,
    UserRole Role,
    DateTimeOffset CreatedAt);
