using CodigoActivo.Application.Abstractions.Querying.ReadModel;

namespace CodigoActivo.Application.Users.Contracts;

/// <summary>
/// Contains the user data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the user.</param>
/// <param name="FirstName">User's given name.</param>
/// <param name="LastName">User's family name.</param>
/// <param name="Email">Email address of the account; dependents never have one.</param>
/// <param name="Phone">Contact phone; dependents never have one.</param>
/// <param name="SecondaryPhone">Optional second contact phone; dependents never have one.</param>
/// <param name="BirthDate">User's date of birth; only dependents have one.</param>
/// <param name="NationalId">Normalized DNI or NIE; only independent accounts have one.</param>
/// <param name="NewsConsent">Whether the user agreed to receive news by email.</param>
/// <param name="Gender">User's gender.</param>
/// <param name="LastLoginAt">UTC timestamp of the user's most recent login; null when they never logged in.</param>
/// <param name="CreatedAt">UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the most recent update; null when never updated.</param>
/// <param name="ParentId">Identifier of the guardian of a dependent; null for an independent account.</param>
/// <param name="ParentName">
/// Full name of the guardian of a dependent; null unless read through
/// <see cref="UserProjections.UserWithType"/>.
/// </param>
/// <param name="DependentCount">
/// Number of dependents of the user; null unless read through <see cref="UserProjections.UserWithType"/>.
/// </param>
/// <param name="Status">Stage of the account: pending verification, active, blocked or dependent.</param>
/// <param name="IsAdmin">Whether the user has administrator rights.</param>
/// <param name="IsInitialAdmin">
/// Whether the account is the initial administrator, which can be neither deleted nor demoted.
/// </param>
/// <param name="Type">
/// Membership type of the user; null unless read through <see cref="UserProjections.UserWithType"/>.
/// </param>
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
    bool NewsConsent,
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
/// Contains the account status of a user, as returned with the user.
/// </summary>
/// <param name="Id">Identifier of the status in the user status catalog.</param>
/// <param name="Name">Display name of the status.</param>
/// <param name="Color">Display color of the status, as a hex code.</param>
public record UserStatusResponse(Guid Id, string Name, string Color);

/// <summary>
/// Contains the membership type of a user, as returned with the user.
/// </summary>
/// <param name="Id">Identifier of the type in the membership type catalog.</param>
/// <param name="Name">Display name of the type.</param>
/// <param name="Color">Display color of the type, as a hex code.</param>
public record UserTypeSummaryResponse(Guid Id, string Name, string Color);

/// <summary>
/// Contains an entry of the user status catalog returned by the API.
/// </summary>
/// <param name="Id">Identifier of the status.</param>
/// <param name="Name">Display name of the status.</param>
/// <param name="Description">Description of the status shown to people.</param>
/// <param name="Color">Display color of the status, as a hex code.</param>
public record UserStatusTypeResponse(Guid Id, string Name, string Description, string Color)
{
    /// <summary>
    /// Initializes an empty user status type response for serialization.
    /// </summary>
    public UserStatusTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty, string.Empty) { }
}

/// <summary>
/// Contains an entry of the membership type catalog returned by the API.
/// </summary>
/// <param name="Id">Identifier of the type.</param>
/// <param name="Name">Display name of the type.</param>
/// <param name="Description">Description of the type shown to people.</param>
/// <param name="Color">Display color of the type, as a hex code.</param>
public record UserTypeResponse(Guid Id, string Name, string Description, string Color)
{
    /// <summary>
    /// Initializes an empty user type response for serialization.
    /// </summary>
    public UserTypeResponse()
        : this(Guid.Empty, string.Empty, string.Empty, string.Empty) { }
}
