using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Validation;

namespace CodigoActivo.Application.DTOs;

public record LoginRequest([Required] string Identifier, [Required] string Password);

public record CsrfTokenResponse(string Token, string HeaderName);

public record RegisterRequest(
    [Required] [MaxLength(120)] [NotBlank] string FirstName,
    [Required] [MaxLength(120)] [NotBlank] string LastName,
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    string Email,
    [Required] [Phone] [MaxLength(40)] string Phone,
    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    string Password,
    [NotDefaultOrFutureDate] DateOnly BirthDate,
    Guid RoleId,
    IReadOnlyList<RegisterMinorRequest>? Minors
);

public record RegisterMinorRequest(
    [Required] [MaxLength(120)] [NotBlank] string FirstName,
    [Required] [MaxLength(120)] [NotBlank] string LastName,
    [NotDefaultOrFutureDate] DateOnly BirthDate,
    Guid RoleId
);

public record RegisterResponse(
    UserResponse Adult,
    IReadOnlyList<UserResponse> Minors,
    Guid? VerificationCode
);