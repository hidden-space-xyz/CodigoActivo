using System.ComponentModel.DataAnnotations;

namespace CodigoActivo.Application.DTOs;

public record LoginRequest(
    [Required] string Identifier,
    [Required] string Password
);

public record CsrfTokenResponse(string Token, string HeaderName);

public record CreateUserRequest(
    [Required, MaxLength(120)] string FirstName,
    [Required, MaxLength(120)] string LastName,
    [EmailAddress, MaxLength(256)] string? Email,
    [Phone, MaxLength(40)] string? Phone,
    [MinLength(8), MaxLength(128)] string? Password,
    DateOnly BirthDate,
    Guid? ParentId,
    Guid RoleId
);

public record CreateUserResponse(UserResponse User, Guid? VerificationCode);
