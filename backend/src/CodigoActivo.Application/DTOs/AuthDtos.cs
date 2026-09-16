using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Validation;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.DTOs;

/// <summary>
/// Contains the client-supplied data used to login.
/// </summary>
/// <param name="Identifier">The identifier value.</param>
/// <param name="Password">Plain-text password to hash or verify.</param>
public record LoginRequest(
    [Required] [MaxLength(256)] [NotBlank] string Identifier,
    [Required] [MaxLength(128)] [NotBlank] string Password
);

/// <summary>
/// Contains the csrf token data returned by the API.
/// </summary>
/// <param name="Token">The token value.</param>
/// <param name="HeaderName">The header name value.</param>
public record CsrfTokenResponse(string Token, string HeaderName);

/// <summary>
/// Contains the client-supplied data used to register.
/// </summary>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="Email">Email address to validate or locate.</param>
/// <param name="Phone">Phone number to validate or locate.</param>
/// <param name="Password">Plain-text password to hash or verify.</param>
/// <param name="BirthDate">User's date of birth.</param>
/// <param name="Gender">The gender value.</param>
/// <param name="Minors">The minors value.</param>
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

/// <summary>
/// Contains the client-supplied data used to register minor.
/// </summary>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="BirthDate">User's date of birth.</param>
/// <param name="Gender">The gender value.</param>
public record RegisterMinorRequest(
    [Required] [MaxLength(120)] [NotBlank] string FirstName,
    [Required] [MaxLength(120)] [NotBlank] string LastName,
    [NotDefaultOrFutureDate] DateOnly BirthDate,
    [EnumDataType(typeof(Gender))] Gender Gender
);

/// <summary>
/// Contains the register data returned by the API.
/// </summary>
/// <param name="Adult">The adult value.</param>
/// <param name="Minors">The minors value.</param>
/// <param name="RequiresVerification">Whether requires verification.</param>
public record RegisterResponse(
    UserResponse Adult,
    IReadOnlyList<UserResponse> Minors,
    bool RequiresVerification
);

/// <summary>
/// Contains the client-supplied data used to verify.
/// </summary>
/// <param name="Otp">The otp value.</param>
public record VerifyRequest([Required] [MaxLength(64)] [NotBlank] string Otp);

/// <summary>
/// Contains the client-supplied data used to forgot password.
/// </summary>
/// <param name="Email">Email address to validate or locate.</param>
public record ForgotPasswordRequest([Required] [EmailAddress] [MaxLength(256)] string Email);

/// <summary>
/// Contains the client-supplied data used to reset password.
/// </summary>
/// <param name="Otp">The otp value.</param>
/// <param name="NewPassword">The new password value.</param>
public record ResetPasswordRequest(
    [Required] [MaxLength(64)] [NotBlank] string Otp,
    [Required] [MinLength(12)] [MaxLength(128)] [NotBlank] string NewPassword
);
