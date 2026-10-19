namespace CodigoActivo.Application.Abstractions.Auditing;

/// <summary>
/// Names the command an audit entry records: one member per audited command, named after it
/// without the <c>Command</c> suffix. Entries store the member name, so members are only ever
/// added, never renamed or removed.
/// </summary>
public enum AuditAction
{
    /// <summary>Started setting up an authenticator app.</summary>
    BeginAuthenticatorSetup,

    /// <summary>Changed a password with the current one.</summary>
    ChangePassword,

    /// <summary>Completed a login with its second factor.</summary>
    CompleteLogin,

    /// <summary>Confirmed an authenticator app.</summary>
    ConfirmAuthenticator,

    /// <summary>Confirmed an email change.</summary>
    ConfirmEmailChange,

    /// <summary>Deleted the signed-in account.</summary>
    DeleteOwnAccount,

    /// <summary>Disabled the authenticator app.</summary>
    DisableAuthenticator,

    /// <summary>Abandoned a pending login.</summary>
    EndLoginChallenge,

    /// <summary>Signed out.</summary>
    EndSession,

    /// <summary>Asked for a password reset.</summary>
    ForgotPassword,

    /// <summary>Passed the password step of a login.</summary>
    Login,

    /// <summary>Registered an account.</summary>
    Register,

    /// <summary>Removed the expired sessions.</summary>
    RemoveExpiredSessions,

    /// <summary>Erased the accounts nobody verified in time.</summary>
    PurgeUnverifiedAccounts,

    /// <summary>Deleted the uploads no content used in time.</summary>
    PurgeUnusedFiles,

    /// <summary>Asked for the code that confirms deleting the own account.</summary>
    RequestAccountDeletionCode,

    /// <summary>Asked for a new login code.</summary>
    ResendTwoFactorCode,

    /// <summary>Asked for a new verification link.</summary>
    ResendVerification,

    /// <summary>Reset a password with an emailed code.</summary>
    ResetPassword,

    /// <summary>Verified an account email.</summary>
    VerifyUser,

    /// <summary>Signed a person up for an activity.</summary>
    AssignActivity,

    /// <summary>Signed a household up for an activity.</summary>
    AssignHousehold,

    /// <summary>Changed the role of a signup.</summary>
    ChangeAssignmentRole,

    /// <summary>Changed the status of a signup.</summary>
    ChangeAssignmentStatus,

    /// <summary>Created an activity.</summary>
    CreateActivity,

    /// <summary>Deleted an activity.</summary>
    DeleteActivity,

    /// <summary>Purged the blocked copies of removed participation past their retention.</summary>
    PurgeDeletedParticipations,

    /// <summary>Withdrew a signup.</summary>
    UnassignActivity,

    /// <summary>Updated an activity.</summary>
    UpdateActivity,

    /// <summary>Emailed the attendees of an event.</summary>
    SendEmailToEventAttendees,

    /// <summary>Emailed one user.</summary>
    SendEmailToUser,

    /// <summary>Emailed a selection of users.</summary>
    SendEmailToUsers,

    /// <summary>Withdrew a news consent through the unsubscribe link of an email.</summary>
    Unsubscribe,

    /// <summary>Created an event category.</summary>
    CreateEventCategoryType,

    /// <summary>Deleted an event category.</summary>
    DeleteEventCategoryType,

    /// <summary>Updated an event category.</summary>
    UpdateEventCategoryType,

    /// <summary>Created an event.</summary>
    CreateEvent,

    /// <summary>Deleted an event.</summary>
    DeleteEvent,

    /// <summary>Featured an event.</summary>
    SetEventFeatured,

    /// <summary>Updated an event.</summary>
    UpdateEvent,

    /// <summary>Withdrew a consent given about a terms document of an event.</summary>
    WithdrawTermsConsent,

    /// <summary>Uploaded a file.</summary>
    CreateFile,

    /// <summary>Deleted a file.</summary>
    DeleteFile,

    /// <summary>Created a news item.</summary>
    CreateNewsItem,

    /// <summary>Deleted a news item.</summary>
    DeleteNewsItem,

    /// <summary>Featured a news item.</summary>
    SetNewsItemFeatured,

    /// <summary>Updated a news item.</summary>
    UpdateNewsItem,

    /// <summary>Created a partner.</summary>
    CreatePartner,

    /// <summary>Deleted a partner.</summary>
    DeletePartner,

    /// <summary>Updated a partner.</summary>
    UpdatePartner,

    /// <summary>Created a resource.</summary>
    CreateResource,

    /// <summary>Deleted a resource.</summary>
    DeleteResource,

    /// <summary>Updated a resource.</summary>
    UpdateResource,

    /// <summary>Created a terms document.</summary>
    CreateTermsDocument,

    /// <summary>Deleted a terms document.</summary>
    DeleteTermsDocument,

    /// <summary>Updated a terms document.</summary>
    UpdateTermsDocument,

    /// <summary>Added a dependent to a household.</summary>
    AddChild,

    /// <summary>Changed the type of a user.</summary>
    ChangeUserType,

    /// <summary>Deleted a user.</summary>
    DeleteUser,

    /// <summary>Erased the dependents who came of age.</summary>
    PurgeAdultDependents,

    /// <summary>Purged the legal copies past their retention.</summary>
    PurgeDeletedAccounts,

    /// <summary>Reset the second factor of a user.</summary>
    ResetTwoFactor,

    /// <summary>Told guardians which dependents come of age soon.</summary>
    SendComingOfAgeNotices,

    /// <summary>Granted or removed administrator rights.</summary>
    SetAdmin,

    /// <summary>Updated a user.</summary>
    UpdateUser,
}
