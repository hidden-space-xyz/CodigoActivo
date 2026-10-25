namespace CodigoActivo.Application.Common.Errors;

/// <summary>
/// Identifies the failures a use case reports beyond the rules of the domain.
/// </summary>
public enum ApplicationErrorCode
{
    /// <summary>
    /// Selects the news item not found option.
    /// </summary>
    NewsItemNotFound,

    /// <summary>
    /// Selects the news item thumbnail not found option.
    /// </summary>
    NewsItemThumbnailNotFound,

    /// <summary>
    /// Selects the activity not found option.
    /// </summary>
    ActivityNotFound,

    /// <summary>
    /// Selects the activity household assignments required option.
    /// </summary>
    ActivityHouseholdAssignmentsRequired,

    /// <summary>
    /// Selects the activity household member not allowed option.
    /// </summary>
    ActivityHouseholdMemberNotAllowed,

    /// <summary>
    /// Selects the activity thumbnail not found option.
    /// </summary>
    ActivityThumbnailNotFound,

    /// <summary>
    /// Selects the event not found option.
    /// </summary>
    EventNotFound,

    /// <summary>
    /// Selects the event activities outside new range option.
    /// </summary>
    EventActivitiesOutsideNewRange,

    /// <summary>
    /// Selects the event category type not found option.
    /// </summary>
    EventCategoryTypeNotFound,

    /// <summary>
    /// Selects the event category type name already exists option.
    /// </summary>
    EventCategoryTypeNameAlreadyExists,

    /// <summary>
    /// Selects the event thumbnail not found option.
    /// </summary>
    EventThumbnailNotFound,

    /// <summary>
    /// A file embedded in the rich text of content no longer exists.
    /// </summary>
    RichTextFileNotFound,

    /// <summary>
    /// Selects the event rating not finished option.
    /// </summary>
    EventRatingNotFinished,

    /// <summary>
    /// Selects the event rating attendance required option.
    /// </summary>
    EventRatingAttendanceRequired,

    /// <summary>
    /// The signed-in account has not rated the event.
    /// </summary>
    EventRatingNotFound,

    /// <summary>
    /// Selects the event terms acceptance required option.
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
    /// Selects the terms document not found option.
    /// </summary>
    TermsDocumentNotFound,

    /// <summary>
    /// No diagnostic log entry has the requested identifier, or it is past its retention.
    /// </summary>
    DiagnosticLogEntryNotFound,

    /// <summary>
    /// Selects the terms document name already exists option.
    /// </summary>
    TermsDocumentNameAlreadyExists,

    /// <summary>
    /// Selects the terms document in use option.
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
    /// Selects the resource not found option.
    /// </summary>
    ResourceNotFound,

    /// <summary>
    /// Selects the resource thumbnail not found option.
    /// </summary>
    ResourceThumbnailNotFound,

    /// <summary>
    /// Selects the partner not found option.
    /// </summary>
    PartnerNotFound,

    /// <summary>
    /// Selects the partner thumbnail not found option.
    /// </summary>
    PartnerThumbnailNotFound,

    /// <summary>
    /// Selects the file not found option.
    /// </summary>
    FileNotFound,

    /// <summary>
    /// Selects the file in use option.
    /// </summary>
    FileInUse,

    /// <summary>
    /// Selects the file content missing from storage option.
    /// </summary>
    FileContentMissingFromStorage,

    /// <summary>
    /// Selects the file upload missing option.
    /// </summary>
    FileUploadMissing,

    /// <summary>
    /// Selects the file upload empty option.
    /// </summary>
    FileUploadEmpty,

    /// <summary>
    /// Selects the file upload too large option.
    /// </summary>
    FileUploadTooLarge,

    /// <summary>
    /// Selects the file upload stream not seekable option.
    /// </summary>
    FileUploadStreamNotSeekable,

    /// <summary>
    /// Selects the file upload unsupported format option.
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
    /// Selects the user not found option.
    /// </summary>
    UserNotFound,

    /// <summary>
    /// Selects the user self delete requires verification option.
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
    /// Selects the parent user not found option.
    /// </summary>
    ParentUserNotFound,

    /// <summary>
    /// Selects the user password not set option.
    /// </summary>
    UserPasswordNotSet,

    /// <summary>
    /// Selects the user current password incorrect option.
    /// </summary>
    UserCurrentPasswordIncorrect,

    /// <summary>
    /// Selects the user new password same as current option.
    /// </summary>
    UserNewPasswordSameAsCurrent,

    /// <summary>
    /// Selects the option for an account registered or updated with an email another account
    /// already uses.
    /// </summary>
    UserEmailAlreadyInUse,

    /// <summary>
    /// Selects the option for an email address whose domain is on the disposable email domain list,
    /// refused when registering or changing an account email.
    /// </summary>
    DisposableEmailNotAllowed,

    /// <summary>
    /// Selects the invalid credentials option.
    /// </summary>
    InvalidCredentials,

    /// <summary>
    /// Selects the user account blocked option.
    /// </summary>
    UserAccountBlocked,

    /// <summary>
    /// Selects the user account pending verification option.
    /// </summary>
    UserAccountPendingVerification,

    /// <summary>
    /// Selects the current user not found option.
    /// </summary>
    CurrentUserNotFound,

    /// <summary>
    /// Selects the otp invalid or expired option.
    /// </summary>
    OtpInvalidOrExpired,

    /// <summary>
    /// Selects the otp resend cooldown active option.
    /// </summary>
    OtpResendCooldownActive,

    /// <summary>
    /// Selects the password reset invalid or expired option.
    /// </summary>
    PasswordResetInvalidOrExpired,

    /// <summary>
    /// Selects the two factor code invalid option.
    /// </summary>
    TwoFactorCodeInvalid,

    /// <summary>
    /// Selects the two factor challenge expired option.
    /// </summary>
    TwoFactorChallengeExpired,

    /// <summary>
    /// Selects the two factor resend cooldown active option.
    /// </summary>
    TwoFactorResendCooldownActive,

    /// <summary>
    /// Selects the authenticator not enabled option.
    /// </summary>
    AuthenticatorNotEnabled,

    /// <summary>
    /// Selects the email no recipients option.
    /// </summary>
    EmailNoRecipients,

    /// <summary>
    /// Selects the email recipient without address option.
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
    /// Selects the email too many recipients option.
    /// </summary>
    EmailTooManyRecipients,

    /// <summary>
    /// Selects the email too many attachments option.
    /// </summary>
    EmailTooManyAttachments,

    /// <summary>
    /// Selects the email attachment empty option.
    /// </summary>
    EmailAttachmentEmpty,

    /// <summary>
    /// Selects the email attachments too large option.
    /// </summary>
    EmailAttachmentsTooLarge,

    /// <summary>
    /// Selects the email send failed option.
    /// </summary>
    EmailSendFailed,

    /// <summary>
    /// A command or query breaks a rule of its input shape, such as a missing or too long value.
    /// </summary>
    MessageInvalid,

    /// <summary>
    /// The signed-in user tried to act for someone who is neither themselves nor one of their
    /// dependents, without being an administrator.
    /// </summary>
    ActingForAnotherUserForbidden,

    /// <summary>
    /// Only the administrator designated for data protection may consult the blocked data.
    /// </summary>
    BlockedDataOfficerOnly,

    /// <summary>
    /// The account designated for data protection must use an authenticator app to consult the
    /// blocked data.
    /// </summary>
    BlockedDataAuthenticatorRequired,

    /// <summary>
    /// An extract was narrowed to a kind of data that keeps no history.
    /// </summary>
    BlockedDataEntityUnknown,

    /// <summary>
    /// An extract was narrowed to days whose first one comes after the last one.
    /// </summary>
    BlockedDataPeriodInvalid,
}
