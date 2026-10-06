using CodigoActivo.Application.Accounts.Contracts;

namespace CodigoActivo.Application.Users.Contracts;

/// <summary>
/// Contains the user data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="Email">Email address to validate or locate.</param>
/// <param name="Phone">Phone number to validate or locate.</param>
/// <param name="SecondaryPhone">Optional second contact phone; dependents never have one.</param>
/// <param name="BirthDate">User's date of birth; only dependents have one.</param>
/// <param name="NationalId">Normalized DNI or NIE; only independent accounts have one.</param>
/// <param name="PromotionalConsent">Whether the user agreed to receive promotional content.</param>
/// <param name="Gender">The gender value.</param>
/// <param name="LastLoginAt">UTC timestamp of the user's most recent login.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update.</param>
/// <param name="ParentId">Identifier of the parent.</param>
/// <param name="ParentName">The parent name value.</param>
/// <param name="DependentCount">Number of dependents linked to the user.</param>
/// <param name="Status">The status value.</param>
/// <param name="IsAdmin">Whether admin.</param>
/// <param name="IsInitialAdmin">
/// Whether the account is the initial administrator, which can be neither deleted nor demoted.
/// </param>
/// <param name="Type">The type value.</param>
/// <param name="TwoFactorMethod">Second factor the user presents when logging in.</param>
/// <param name="EarlySignupEligible">
/// Whether the account may sign up during the early signup: its membership type, or its guardian's for
/// a dependent, is entitled to it.
/// </param>
public record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? SecondaryPhone,
    DateOnly? BirthDate,
    string? NationalId,
    bool PromotionalConsent,
    Gender Gender,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid? ParentId,
    string? ParentName,
    int? DependentCount,
    UserStatusResponse Status,
    bool IsAdmin,
    bool IsInitialAdmin,
    UserTypeSummaryResponse? Type,
    TwoFactorMethod TwoFactorMethod,
    bool EarlySignupEligible
)
{
    /// <summary>
    /// Initializes an empty user response for serialization.
    /// </summary>
    public UserResponse()
        : this(
            Guid.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            null,
            null,
            false,
            default,
            null,
            default,
            null,
            null,
            null,
            null,
            null!,
            false,
            false,
            null,
            TwoFactorMethod.Email,
            false
        ) { }
}

/// <summary>
/// Contains the user status data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Color">The color value.</param>
public record UserStatusResponse(Guid Id, string Name, string Color);

/// <summary>
/// Contains the user type data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Color">The color value.</param>
public record UserTypeSummaryResponse(Guid Id, string Name, string Color);

/// <summary>
/// Contains the user status type data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Color">The color value.</param>
public record UserStatusTypeResponse(Guid Id, string Name, string Description, string Color)
{
    /// <summary>
    /// Initializes an empty user status type response for serialization.
    /// </summary>
    public UserStatusTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty, string.Empty) { }
}

/// <summary>
/// Contains the user type data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Description">The description value.</param>
/// <param name="Color">The color value.</param>
public record UserTypeResponse(Guid Id, string Name, string Description, string Color)
{
    /// <summary>
    /// Initializes an empty user type response for serialization.
    /// </summary>
    public UserTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty, string.Empty) { }
}
