using System.ComponentModel.DataAnnotations;

namespace CodigoActivo.Application.DTOs;

public record LoginRequest(
    [Required] string Identifier,
    [Required] string Password
);

public record CsrfTokenResponse(string Token, string HeaderName);

/// <summary>
/// Household registration: an adult registers themselves and, optionally, the
/// minors in their care. One <c>User</c> is created per person; minors are
/// linked to the adult via <c>ParentId</c>.
/// </summary>
public record RegisterRequest(
    [Required, MaxLength(120)] string FirstName,
    [Required, MaxLength(120)] string LastName,
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, Phone, MaxLength(40)] string Phone,
    [Required, MinLength(8), MaxLength(128)] string Password,
    DateOnly BirthDate,
    Guid RoleId,
    IReadOnlyList<RegisterMinorRequest>? Minors
);

public record RegisterMinorRequest(
    [Required, MaxLength(120)] string FirstName,
    [Required, MaxLength(120)] string LastName,
    DateOnly BirthDate,
    Guid RoleId
);

public record RegisterResponse(
    UserResponse Adult,
    IReadOnlyList<UserResponse> Minors,
    Guid? VerificationCode
);
