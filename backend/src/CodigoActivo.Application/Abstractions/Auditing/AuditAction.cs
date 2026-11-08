namespace CodigoActivo.Application.Abstractions.Auditing;

/// <summary>
/// Names what an audit entry records: one member per audited command or query, named after it
/// without its <c>Command</c> or <c>Query</c> suffix, and one per security event the application
/// detects on its own. Entries store the member name, so members are only ever added, never renamed
/// or removed.
/// </summary>
public enum AuditAction
{
    /// <summary>
    /// Started setting up an authenticator app.
    /// </summary>
    BeginAuthenticatorSetup,

    /// <summary>
    /// Changed a password with the current one.
    /// </summary>
    ChangePassword,

    /// <summary>
    /// Completed a login with its second factor.
    /// </summary>
    CompleteLogin,

    /// <summary>
    /// Confirmed an authenticator app.
    /// </summary>
    ConfirmAuthenticator,

    /// <summary>
    /// Confirmed an email change.
    /// </summary>
    ConfirmEmailChange,

    /// <summary>
    /// Deleted the signed-in account.
    /// </summary>
    DeleteOwnAccount,

    /// <summary>
    /// Disabled the authenticator app.
    /// </summary>
    DisableAuthenticator,

    /// <summary>
    /// Signed out.
    /// </summary>
    EndSession,

    /// <summary>
    /// Asked for a password reset.
    /// </summary>
    ForgotPassword,

    /// <summary>
    /// Passed the password step of a login.
    /// </summary>
    Login,

    /// <summary>
    /// Registered an account.
    /// </summary>
    Register,

    /// <summary>
    /// Erased the accounts nobody verified in time.
    /// </summary>
    PurgeUnverifiedAccounts,

    /// <summary>
    /// Asked for the code that confirms deleting the own account.
    /// </summary>
    RequestAccountDeletionCode,

    /// <summary>
    /// Reset a password with an emailed code.
    /// </summary>
    ResetPassword,

    /// <summary>
    /// Verified an account email.
    /// </summary>
    VerifyUser,

    /// <summary>
    /// Signed a person up for an activity.
    /// </summary>
    AssignActivity,

    /// <summary>
    /// Signed a household up for an activity.
    /// </summary>
    AssignHousehold,

    /// <summary>
    /// Changed the role of a signup.
    /// </summary>
    ChangeAssignmentRole,

    /// <summary>
    /// Changed the status of a signup.
    /// </summary>
    ChangeAssignmentStatus,

    /// <summary>
    /// Created an activity.
    /// </summary>
    CreateActivity,

    /// <summary>
    /// Deleted an activity.
    /// </summary>
    DeleteActivity,

    /// <summary>
    /// Withdrew a signup.
    /// </summary>
    UnassignActivity,

    /// <summary>
    /// Updated an activity.
    /// </summary>
    UpdateActivity,

    /// <summary>
    /// Emailed the attendees of an event.
    /// </summary>
    SendEmailToEventAttendees,

    /// <summary>
    /// Emailed one user.
    /// </summary>
    SendEmailToUser,

    /// <summary>
    /// Emailed a selection of users.
    /// </summary>
    SendEmailToUsers,

    /// <summary>
    /// Withdrew a news consent through the unsubscribe link of an email.
    /// </summary>
    Unsubscribe,

    /// <summary>
    /// Created an event category.
    /// </summary>
    CreateEventCategoryType,

    /// <summary>
    /// Deleted an event category.
    /// </summary>
    DeleteEventCategoryType,

    /// <summary>
    /// Updated an event category.
    /// </summary>
    UpdateEventCategoryType,

    /// <summary>
    /// Created an event.
    /// </summary>
    CreateEvent,

    /// <summary>
    /// Deleted an event.
    /// </summary>
    DeleteEvent,

    /// <summary>
    /// Updated an event.
    /// </summary>
    UpdateEvent,

    /// <summary>
    /// Withdrew a consent given about a terms document of an event.
    /// </summary>
    WithdrawTermsConsent,

    /// <summary>
    /// Uploaded a file.
    /// </summary>
    CreateFile,

    /// <summary>
    /// Deleted a file.
    /// </summary>
    DeleteFile,

    /// <summary>
    /// Created a news item.
    /// </summary>
    CreateNewsItem,

    /// <summary>
    /// Deleted a news item.
    /// </summary>
    DeleteNewsItem,

    /// <summary>
    /// Updated a news item.
    /// </summary>
    UpdateNewsItem,

    /// <summary>
    /// Created a partner.
    /// </summary>
    CreatePartner,

    /// <summary>
    /// Deleted a partner.
    /// </summary>
    DeletePartner,

    /// <summary>
    /// Updated a partner.
    /// </summary>
    UpdatePartner,

    /// <summary>
    /// Created a resource.
    /// </summary>
    CreateResource,

    /// <summary>
    /// Deleted a resource.
    /// </summary>
    DeleteResource,

    /// <summary>
    /// Updated a resource.
    /// </summary>
    UpdateResource,

    /// <summary>
    /// Created a terms document.
    /// </summary>
    CreateTermsDocument,

    /// <summary>
    /// Deleted a terms document.
    /// </summary>
    DeleteTermsDocument,

    /// <summary>
    /// Updated a terms document.
    /// </summary>
    UpdateTermsDocument,

    /// <summary>
    /// Published a new version of a legal policy.
    /// </summary>
    PublishLegalPolicy,

    /// <summary>
    /// Added a dependent to a household.
    /// </summary>
    AddChild,

    /// <summary>
    /// Changed the membership type of a user.
    /// </summary>
    ChangeUserType,

    /// <summary>
    /// Deleted a user.
    /// </summary>
    DeleteUser,

    /// <summary>
    /// Erased the dependents who came of age.
    /// </summary>
    PurgeAdultDependents,

    /// <summary>
    /// Reset the second factor of a user.
    /// </summary>
    ResetTwoFactor,

    /// <summary>
    /// Told guardians which dependents come of age soon.
    /// </summary>
    SendComingOfAgeNotices,

    /// <summary>
    /// Changed the staff role of a user.
    /// </summary>
    SetStaffRole,

    /// <summary>
    /// Designated the account for data protection.
    /// </summary>
    DesignatePrivacyOfficer,

    /// <summary>
    /// Updated a user.
    /// </summary>
    UpdateUser,

    /// <summary>
    /// Purged the history, consultations and holds past their retention.
    /// </summary>
    PurgeDataHistory,

    /// <summary>
    /// Asked for the code that confirms opening a consultation of the blocked data.
    /// </summary>
    RequestBlockedDataConsultationCode,

    /// <summary>
    /// Opened a consultation of the blocked data.
    /// </summary>
    OpenBlockedDataConsultation,

    /// <summary>
    /// Looked accounts up in a consultation of the blocked data.
    /// </summary>
    SearchBlockedData,

    /// <summary>
    /// Took an extract of the blocked data about an account.
    /// </summary>
    ExportBlockedData,

    /// <summary>
    /// Held the data history of an account while a claim about it is open.
    /// </summary>
    PlaceBlockedDataHold,

    /// <summary>
    /// Lifted a hold on the data history once its claim closed.
    /// </summary>
    LiftBlockedDataHold,

    /// <summary>
    /// Told the legal contact about the consents withdrawn since the last notice.
    /// </summary>
    SendConsentWithdrawalNotices,

    /// <summary>
    /// Told the legal contact about the consultations of the blocked data opened since the last
    /// notice.
    /// </summary>
    SendBlockedDataConsultationNotices,

    /// <summary>
    /// Too many wrong passwords locked an account.
    /// </summary>
    PasswordLocked,

    /// <summary>
    /// Too many wrong codes locked the second factor of an account.
    /// </summary>
    TwoFactorLocked,

    /// <summary>
    /// A signed-in account was refused something its role or ties do not allow.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// Took the copy of the own personal data.
    /// </summary>
    GetPersonalData,

    /// <summary>
    /// Opened the signature of a terms decision.
    /// </summary>
    GetEventTermsSignature,

    /// <summary>
    /// Opened the roster of an event, with the contact details of its attendees.
    /// </summary>
    GetEventRoster,

    /// <summary>
    /// Opened the badges of an event.
    /// </summary>
    GetEventBadges,
}
