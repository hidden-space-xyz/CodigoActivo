using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Validation;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.DTOs;

public record LoginRequest(
    [Required] [MaxLength(256)] [NotBlank] string Identifier,
    [Required] [MaxLength(128)] [NotBlank] string Password
);

public record CsrfTokenResponse(string Token, string HeaderName);

public record RegisterRequest(
    [Required] [MaxLength(120)] [NotBlank] string FirstName,
    [Required] [MaxLength(120)] [NotBlank] string LastName,
    [Required] [EmailAddress] [MaxLength(256)] string Email,
    [Required] [Phone] [MaxLength(40)] string Phone,
    [Required] [MinLength(12)] [MaxLength(128)] [NotBlank] string Password,
    [NotDefaultOrFutureDate] DateOnly BirthDate,
    [EnumDataType(typeof(Gender))] Gender Gender,
    [MaxLength(20)] IReadOnlyList<RegisterMinorRequest>? Minors
);

public record RegisterMinorRequest(
    [Required] [MaxLength(120)] [NotBlank] string FirstName,
    [Required] [MaxLength(120)] [NotBlank] string LastName,
    [NotDefaultOrFutureDate] DateOnly BirthDate,
    [EnumDataType(typeof(Gender))] Gender Gender
);

public record RegisterResponse(
    UserResponse Adult,
    IReadOnlyList<UserResponse> Minors,
    bool RequiresVerification
);

public record VerifyRequest([Required] [MaxLength(64)] [NotBlank] string Otp);

public record ForgotPasswordRequest([Required] [EmailAddress] [MaxLength(256)] string Email);

public record ResetPasswordRequest(
    [Required] [MaxLength(64)] [NotBlank] string Otp,
    [Required] [MinLength(12)] [MaxLength(128)] [NotBlank] string NewPassword
);
