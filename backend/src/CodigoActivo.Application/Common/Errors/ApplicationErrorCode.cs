namespace CodigoActivo.Application.Common.Errors;

/// <summary>
/// Identifies the failures a use case reports beyond the rules of the domain.
/// </summary>
public enum ApplicationErrorCode
{
    /// <summary>
    /// No news item has the requested identifier.
    /// </summary>
    NewsItemNotFound,

    /// <summary>
    /// The thumbnail chosen for a news item no longer exists.
    /// </summary>
    NewsItemThumbnailNotFound,

    /// <summary>
    /// No activity has the requested identifier.
    /// </summary>
    ActivityNotFound,

    /// <summary>
    /// A household signup lists no member.
    /// </summary>
    ActivityHouseholdAssignmentsRequired,

    /// <summary>
    /// A household signup lists someone who does not exist or is neither the signed-in user nor one
    /// of their dependents.
    /// </summary>
    ActivityHouseholdMemberNotAllowed,

    /// <summary>
    /// The thumbnail chosen for an activity no longer exists.
    /// </summary>
    ActivityThumbnailNotFound,

    /// <summary>
    /// No event has the requested identifier, such as the one an activity is placed in.
    /// </summary>
    EventNotFound,

    /// <summary>
    /// The new dates of an event would leave some of its activities outside them.
    /// </summary>
    EventActivitiesOutsideNewRange,

    /// <summary>
    /// The event category type asked for, or one selected for an event, does not exist.
    /// </summary>
    EventCategoryTypeNotFound,

    /// <summary>
    /// Another event category type already has that name.
    /// </summary>
    EventCategoryTypeNameAlreadyExists,

    /// <summary>
    /// The thumbnail chosen for an event no longer exists.
    /// </summary>
    EventThumbnailNotFound,

    /// <summary>
    /// A file embedded in the rich text of content no longer exists.
    /// </summary>
    RichTextFileNotFound,

    /// <summary>
    /// The event cannot be rated before it ends.
    /// </summary>
    EventRatingNotFinished,

    /// <summary>
    /// Neither the signed-in user nor any of their dependents has a confirmed signup to an activity
    /// of the event they rate.
    /// </summary>
    EventRatingAttendanceRequired,

    /// <summary>
    /// The signed-in account has not rated the event.
    /// </summary>
    EventRatingNotFound,

    /// <summary>
    /// A signup leaves a required terms document of the event unaccepted.
    /// </summary>
    EventTermsAcceptanceRequired,

    /// <summary>
    /// The terms decision asked for does not exist or was taken without a signature.
    /// </summary>
    EventTermsSignatureNotFound,

    /// <summary>
    /// No decision was taken about that document of the event for that person.
    /// </summary>
    EventTermsDecisionNotFound,

    /// <summary>
    /// The terms document asked for, or one linked to an event, does not exist.
    /// </summary>
    TermsDocumentNotFound,

    /// <summary>
    /// No diagnostic log entry has the requested identifier, or it is past its retention.
    /// </summary>
    DiagnosticLogEntryNotFound,

    /// <summary>
    /// Another terms document already has that name.
    /// </summary>
    TermsDocumentNameAlreadyExists,

    /// <summary>
    /// A terms document cannot be deleted while an event links it or anyone decided on it.
    /// </summary>
    TermsDocumentInUse,

    /// <summary>
    /// The kind of a terms document cannot change while an event links it or anyone decided on it.
    /// </summary>
    TermsDocumentKindInUse,

    /// <summary>
    /// No policy of that kind was published yet.
    /// </summary>
    LegalPolicyNotFound,

    /// <summary>
    /// The policy has no version with that number.
    /// </summary>
    LegalPolicyVersionNotFound,

    /// <summary>
    /// Another administrator published the first version of the same policy at the same time.
    /// </summary>
    LegalPolicyPublishedConcurrently,

    /// <summary>
    /// No resource has the requested identifier.
    /// </summary>
    ResourceNotFound,

    /// <summary>
    /// The thumbnail chosen for a resource no longer exists.
    /// </summary>
    ResourceThumbnailNotFound,

    /// <summary>
    /// No partner has the requested identifier.
    /// </summary>
    PartnerNotFound,

    /// <summary>
    /// The thumbnail chosen for a partner no longer exists.
    /// </summary>
    PartnerThumbnailNotFound,

    /// <summary>
    /// No stored file has the requested identifier.
    /// </summary>
    FileNotFound,

    /// <summary>
    /// A file cannot be deleted while some content still references it.
    /// </summary>
    FileInUse,

    /// <summary>
    /// The file exists but the content of the variant asked for is missing from storage.
    /// </summary>
    FileContentMissingFromStorage,

    /// <summary>
    /// No file was uploaded.
    /// </summary>
    FileUploadMissing,

    /// <summary>
    /// The uploaded file is empty.
    /// </summary>
    FileUploadEmpty,

    /// <summary>
    /// The uploaded file is larger than <c>FileUploadOptions.MaxSizeBytes</c>.
    /// </summary>
    FileUploadTooLarge,

    /// <summary>
    /// The content of the upload cannot be rewound to detect its format.
    /// </summary>
    FileUploadStreamNotSeekable,

    /// <summary>
    /// The uploaded file is not a JPEG, PNG or WebP image.
    /// </summary>
    FileUploadUnsupportedFormat,

    /// <summary>
    /// An uploaded image declares more pixels than the upload limit allows.
    /// </summary>
    FileUploadDimensionsTooLarge,

    /// <summary>
    /// An uploaded image could not be decoded.
    /// </summary>
    FileUploadInvalidImage,

    /// <summary>
    /// The account acted on, the signed-in one included, does not exist or no longer does.
    /// </summary>
    UserNotFound,

    /// <summary>
    /// The signed-in user tried to delete their own account the way another account is deleted,
    /// instead of through the self-deletion, which asks for the password and the second factor.
    /// </summary>
    UserSelfDeleteRequiresVerification,

    /// <summary>
    /// Selects the option for a user who grants or revokes their own administrator rights.
    /// </summary>
    UserSelfAdminChangeForbidden,

    /// <summary>
    /// Selects the option for a user who changes their own membership type.
    /// </summary>
    UserSelfTypeChangeForbidden,

    /// <summary>
    /// The guardian a dependent is added to does not exist.
    /// </summary>
    ParentUserNotFound,

    /// <summary>
    /// The account whose password would change has none.
    /// </summary>
    UserPasswordNotSet,

    /// <summary>
    /// The password of the signed-in user, asked to confirm a sensitive change, is wrong.
    /// </summary>
    UserCurrentPasswordIncorrect,

    /// <summary>
    /// The new password is the same as the current one.
    /// </summary>
    UserNewPasswordSameAsCurrent,

    /// <summary>
    /// The new email being confirmed already belongs to another account that owns it. Registering
    /// with, or asking to change to, an email in use reports nothing: its holder is notified instead.
    /// </summary>
    UserEmailAlreadyInUse,

    /// <summary>
    /// Selects the option for an email address whose domain is on the disposable email domain list,
    /// refused when registering or changing an account email.
    /// </summary>
    DisposableEmailNotAllowed,

    /// <summary>
    /// The email and password given to sign in do not match an account.
    /// </summary>
    InvalidCredentials,

    /// <summary>
    /// The account signing in with the right password is blocked.
    /// </summary>
    UserAccountBlocked,

    /// <summary>
    /// The account signing in with the right password has not confirmed its email yet.
    /// </summary>
    UserAccountPendingVerification,

    /// <summary>
    /// The account of the signed-in user no longer exists when it is read; answered as unauthorized.
    /// </summary>
    CurrentUserNotFound,

    /// <summary>
    /// The code that confirms the email of a new account, or a new email, is wrong, expired, already
    /// used or not pending.
    /// </summary>
    OtpInvalidOrExpired,

    /// <summary>
    /// The email guard refused the email that confirms a new email address.
    /// </summary>
    OtpResendCooldownActive,

    /// <summary>
    /// The password reset code is wrong, expired or already used, or the account is blocked or a
    /// dependent.
    /// </summary>
    PasswordResetInvalidOrExpired,

    /// <summary>
    /// The second-factor code is wrong, expired or already used, or the first code of an
    /// authenticator being set up does not match its key.
    /// </summary>
    TwoFactorCodeInvalid,

    /// <summary>
    /// The login challenge is unknown, expired or no longer open, or its account no longer exists.
    /// </summary>
    TwoFactorChallengeExpired,

    /// <summary>
    /// A new login code was asked for before the resend cooldown ended, or the email guard refused
    /// it.
    /// </summary>
    TwoFactorResendCooldownActive,

    /// <summary>
    /// The signed-in account does not use an authenticator application as its second factor.
    /// </summary>
    AuthenticatorNotEnabled,

    /// <summary>
    /// A manual email is left without any recipient.
    /// </summary>
    EmailNoRecipients,

    /// <summary>
    /// The account a manual email is written to has no email address.
    /// </summary>
    EmailRecipientWithoutAddress,

    /// <summary>
    /// An email was written to an account that has not confirmed its email address.
    /// </summary>
    EmailRecipientNotVerified,

    /// <summary>
    /// A news email was written to someone without news consent.
    /// </summary>
    EmailRecipientWithoutConsent,

    /// <summary>
    /// The unsubscribe link of a news email was altered or not issued here.
    /// </summary>
    EmailUnsubscribeLinkInvalid,

    /// <summary>
    /// A manual email has more recipients than <c>ManualEmailOptions.MaxRecipients</c>.
    /// </summary>
    EmailTooManyRecipients,

    /// <summary>
    /// A manual email has more attachments than <c>ManualEmailOptions.MaxAttachments</c>.
    /// </summary>
    EmailTooManyAttachments,

    /// <summary>
    /// An attachment of a manual email is empty.
    /// </summary>
    EmailAttachmentEmpty,

    /// <summary>
    /// The attachments of a manual email add up to more than
    /// <c>ManualEmailOptions.MaxAttachmentsBytes</c>.
    /// </summary>
    EmailAttachmentsTooLarge,

    /// <summary>
    /// An email could not be queued for delivery: a login code or the confirmation of a new email
    /// failed to compose or store, or the outbox could not store or had no room for a manual email.
    /// </summary>
    EmailSendFailed,

    /// <summary>
    /// A command or query breaks a rule of its input shape, such as a missing or too long value.
    /// </summary>
    MessageInvalid,

    /// <summary>
    /// The signed-in user tried to act for someone who is neither themselves nor one of their
    /// dependents, without being an administrator. Personal data, the news consent and dependents
    /// refuse administrators too, and only its holder changes a password.
    /// </summary>
    ActingForAnotherUserForbidden,

    /// <summary>
    /// Only the administrator designated for data protection may consult the blocked data.
    /// </summary>
    BlockedDataOfficerOnly,

    /// <summary>
    /// An extract was narrowed to a kind of data that keeps no history.
    /// </summary>
    BlockedDataEntityUnknown,

    /// <summary>
    /// An extract was narrowed to days whose first one comes after the last one.
    /// </summary>
    BlockedDataPeriodInvalid,
}
