using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Logs.Contracts;

namespace CodigoActivo.Application.Accounts.Contracts;

/// <summary>
/// Represents a copy of the personal data kept about the signed-in user and the minors in their
/// care, in a structured format they can read or take elsewhere (articles 15 and 20 GDPR).
/// Ratings are left out because nothing links them to an account, and so are the security
/// secrets of the account and the other people and things the audit entries touched.
/// </summary>
/// <param name="GeneratedAt">When the copy was made.</param>
/// <param name="Account">The signed-in user's account.</param>
/// <param name="NewsConsentChanges">Every change of the news consent, oldest first.</param>
/// <param name="Dependents">The minors in the user's care.</param>
/// <param name="Signups">The signups of the user and of the minors in their care.</param>
/// <param name="TermsDecisions">The user's decisions on the documents of the events.</param>
/// <param name="Sessions">The user's open sessions.</param>
/// <param name="SecurityLog">
/// The audit entries about the user and the minors in their care that the log still holds.
/// </param>
public sealed record PersonalDataResponse(
    DateTimeOffset GeneratedAt,
    PersonalDataAccountResponse Account,
    IReadOnlyList<PersonalDataConsentChangeResponse> NewsConsentChanges,
    IReadOnlyList<PersonalDataDependentResponse> Dependents,
    IReadOnlyList<PersonalDataSignupResponse> Signups,
    IReadOnlyList<PersonalDataTermsDecisionResponse> TermsDecisions,
    IReadOnlyList<PersonalDataSessionResponse> Sessions,
    IReadOnlyList<AccountAuditEntryResponse> SecurityLog
);

/// <summary>
/// Represents the signed-in user's account in their copy of personal data.
/// </summary>
/// <param name="Id">Identifier of the account.</param>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="Email">Email address.</param>
/// <param name="PendingEmail">New email address asked for and not confirmed yet.</param>
/// <param name="Phone">Main phone number.</param>
/// <param name="SecondaryPhone">Second phone number.</param>
/// <param name="NationalId">DNI or NIE.</param>
/// <param name="Gender">Gender.</param>
/// <param name="NewsConsent">Whether news email is accepted.</param>
/// <param name="UserType">Name of the relationship with the association.</param>
/// <param name="Status">Name of the account status.</param>
/// <param name="IsAdmin">Whether the account administers the site.</param>
/// <param name="TwoFactorMethod">Second factor in use.</param>
/// <param name="CreatedAt">When the account was created.</param>
/// <param name="UpdatedAt">When the account was last updated.</param>
/// <param name="LastLoginAt">When the user last logged in.</param>
/// <param name="AdultDeclaredAt">When the user declared being of age while registering.</param>
public sealed record PersonalDataAccountResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? PendingEmail,
    string? Phone,
    string? SecondaryPhone,
    string? NationalId,
    Gender Gender,
    bool NewsConsent,
    string UserType,
    string Status,
    bool IsAdmin,
    TwoFactorMethod TwoFactorMethod,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? AdultDeclaredAt
);

/// <summary>
/// Represents a change of the news consent in a copy of personal data.
/// </summary>
/// <param name="Granted">Whether the consent was given or withdrawn.</param>
/// <param name="Source">Where it was changed.</param>
/// <param name="ChangedAt">When it was changed.</param>
public sealed record PersonalDataConsentChangeResponse(
    bool Granted,
    string Source,
    DateTimeOffset ChangedAt
);

/// <summary>
/// Represents a minor in the user's care in a copy of personal data.
/// </summary>
/// <param name="Id">Identifier of the minor.</param>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="BirthDate">Date of birth.</param>
/// <param name="Gender">Gender.</param>
/// <param name="UserType">Name of the relationship with the association.</param>
/// <param name="CreatedAt">When the minor was added.</param>
/// <param name="UpdatedAt">When the minor was last updated.</param>
/// <param name="GuardianshipDeclaredAt">
/// When the user declared being the minor's parent or legal guardian.
/// </param>
/// <param name="ComingOfAgeNoticeSentAt">
/// When the user was told that the minor will be erased the day it comes of age.
/// </param>
public sealed record PersonalDataDependentResponse(
    Guid Id,
    string FirstName,
    string LastName,
    DateOnly? BirthDate,
    Gender Gender,
    string UserType,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? GuardianshipDeclaredAt,
    DateTimeOffset? ComingOfAgeNoticeSentAt
);

/// <summary>
/// Represents a signup in a copy of personal data.
/// </summary>
/// <param name="PersonId">Identifier of the person signed up: the user or a minor in their care.</param>
/// <param name="PersonName">Full name of the person signed up.</param>
/// <param name="EventTitle">Title of the event.</param>
/// <param name="ActivityTitle">Title of the activity.</param>
/// <param name="ActivityStartsAt">When the activity starts.</param>
/// <param name="Role">Name of the role.</param>
/// <param name="Status">Name of the signup status.</param>
/// <param name="SignedUpAt">When the person was signed up.</param>
public sealed record PersonalDataSignupResponse(
    Guid PersonId,
    string PersonName,
    string EventTitle,
    string ActivityTitle,
    DateTimeOffset ActivityStartsAt,
    string Role,
    string Status,
    DateTimeOffset SignedUpAt
);

/// <summary>
/// Represents a decision on a document of an event in a copy of personal data.
/// </summary>
/// <param name="EventTitle">Title of the event.</param>
/// <param name="DocumentName">Name of the document.</param>
/// <param name="Accepted">Whether the document was accepted.</param>
/// <param name="DecidedAt">When it was decided.</param>
/// <param name="Signature">Handwritten signature as a PNG image, when the document asked for one.</param>
public sealed record PersonalDataTermsDecisionResponse(
    string EventTitle,
    string DocumentName,
    bool Accepted,
    DateTimeOffset DecidedAt,
    byte[]? Signature
);

/// <summary>
/// Represents an open session in a copy of personal data.
/// </summary>
/// <param name="CreatedAt">When the session was opened.</param>
/// <param name="ExpiresAt">When the session expires.</param>
public sealed record PersonalDataSessionResponse(
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt
);
