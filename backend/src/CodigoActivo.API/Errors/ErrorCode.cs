namespace CodigoActivo.API.Errors;

/// <summary>
/// Identifies every failure the API reports in <c>ApiErrorResponse.Code</c>. It is the wire contract
/// the frontend translates, so members are never renamed or reordered.
/// </summary>
public enum ErrorCode
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
    /// Selects the activity signup closed option.
    /// </summary>
    ActivitySignupClosed,

    /// <summary>
    /// Selects the activity signup early only option.
    /// </summary>
    ActivitySignupEarlyOnly,

    /// <summary>
    /// Selects the activity already started option.
    /// </summary>
    ActivityAlreadyStarted,

    /// <summary>
    /// Selects the activity role not allowed option.
    /// </summary>
    ActivityRoleNotAllowed,

    /// <summary>
    /// Selects the activity assignment already exists option.
    /// </summary>
    ActivityAssignmentAlreadyExists,

    /// <summary>
    /// Selects the activity household assignments required option.
    /// </summary>
    ActivityHouseholdAssignmentsRequired,

    /// <summary>
    /// Selects the activity household member not allowed option.
    /// </summary>
    ActivityHouseholdMemberNotAllowed,

    /// <summary>
    /// Selects the activity assignment not found option.
    /// </summary>
    ActivityAssignmentNotFound,

    /// <summary>
    /// Selects the activity schedule required option.
    /// </summary>
    ActivityScheduleRequired,

    /// <summary>
    /// Selects the activity schedule invalid range option.
    /// </summary>
    ActivityScheduleInvalidRange,

    /// <summary>
    /// Selects the activity schedule outside event range option.
    /// </summary>
    ActivityScheduleOutsideEventRange,

    /// <summary>
    /// Selects the activity thumbnail not found option.
    /// </summary>
    ActivityThumbnailNotFound,

    /// <summary>
    /// Selects the activity role capacity duplicated option.
    /// </summary>
    ActivityRoleCapacityDuplicated,

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
    /// Selects the event category type only category of event option.
    /// </summary>
    EventCategoryTypeOnlyCategoryOfEvent,

    /// <summary>
    /// Selects the event thumbnail not found option.
    /// </summary>
    EventThumbnailNotFound,

    /// <summary>
    /// Selects the event categories required option.
    /// </summary>
    EventCategoriesRequired,

    /// <summary>
    /// Selects the event schedule required option.
    /// </summary>
    EventScheduleRequired,

    /// <summary>
    /// Selects the event schedule invalid range option.
    /// </summary>
    EventScheduleInvalidRange,

    /// <summary>
    /// Selects the event early signup not before signup option.
    /// </summary>
    EventEarlySignupNotBeforeSignup,

    /// <summary>
    /// Selects the event signup ends after event option.
    /// </summary>
    EventSignupEndsAfterEvent,

    /// <summary>
    /// Selects the event rating not finished option.
    /// </summary>
    EventRatingNotFinished,

    /// <summary>
    /// Selects the event rating attendance required option.
    /// </summary>
    EventRatingAttendanceRequired,

    /// <summary>
    /// Selects the event rating empty option.
    /// </summary>
    EventRatingEmpty,

    /// <summary>
    /// Selects the event terms acceptance required option.
    /// </summary>
    EventTermsAcceptanceRequired,

    /// <summary>
    /// A terms document that asks for a signature was accepted without one.
    /// </summary>
    EventTermsSignatureRequired,

    /// <summary>
    /// The terms decision asked for does not exist or was taken without a signature.
    /// </summary>
    EventTermsSignatureNotFound,

    /// <summary>
    /// Selects the event terms document duplicated option.
    /// </summary>
    EventTermsDocumentDuplicated,

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
    /// Selects the resource not found option.
    /// </summary>
    ResourceNotFound,

    /// <summary>
    /// Selects the resource thumbnail not found option.
    /// </summary>
    ResourceThumbnailNotFound,

    /// <summary>
    /// Selects the resource description required option.
    /// </summary>
    ResourceDescriptionRequired,

    /// <summary>
    /// Selects the resource description not allowed option.
    /// </summary>
    ResourceDescriptionNotAllowed,

    /// <summary>
    /// Selects the resource url required option.
    /// </summary>
    ResourceUrlRequired,

    /// <summary>
    /// Selects the resource url not allowed option.
    /// </summary>
    ResourceUrlNotAllowed,

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
    /// Selects the user delete initial admin forbidden option.
    /// </summary>
    UserDeleteInitialAdminForbidden,

    /// <summary>
    /// Selects the user cannot remove initial admin option.
    /// </summary>
    UserCannotRemoveInitialAdmin,

    /// <summary>
    /// Selects the option for administrator rights given to a dependent.
    /// </summary>
    UserDependentCannotBeAdmin,

    /// <summary>
    /// Selects the parent user not found option.
    /// </summary>
    ParentUserNotFound,

    /// <summary>
    /// Selects the option for a parent that is itself a dependent.
    /// </summary>
    UserParentIsMinor,

    /// <summary>
    /// Selects the option for a dependent created, when registering or later, or given a new birth
    /// date that does not make it a minor.
    /// </summary>
    UserChildBirthDateNotMinor,

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
    /// Selects the user parent reassignment forbidden option.
    /// </summary>
    UserParentReassignmentForbidden,

    /// <summary>
    /// Selects the user child limit reached option.
    /// </summary>
    UserChildLimitReached,

    /// <summary>
    /// Selects the user parent not allowed for adult option.
    /// </summary>
    UserParentNotAllowedForAdult,

    /// <summary>
    /// Selects the option for a birth date given to an account that is not a dependent.
    /// </summary>
    UserBirthDateNotAllowedForAdult,

    /// <summary>
    /// Selects the option for a dependent updated without a birth date.
    /// </summary>
    UserChildBirthDateRequired,

    /// <summary>
    /// Selects the option for an independent account registered or updated without an email or a
    /// phone, or asked for an emailed code while it has no email.
    /// </summary>
    UserContactInfoRequired,

    /// <summary>
    /// Selects the option for an independent account registered or updated without a DNI or NIE.
    /// </summary>
    UserNationalIdRequired,

    /// <summary>
    /// Selects the option for an account registered or updated with an email another account
    /// already uses.
    /// </summary>
    UserEmailAlreadyInUse,

    /// <summary>
    /// Selects the option for a secondary phone equal to the main phone of the same account.
    /// </summary>
    SecondaryPhoneSameAsPrimary,

    /// <summary>
    /// Selects the user phone invalid option.
    /// </summary>
    UserPhoneInvalid,

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
    /// Selects the two factor locked option.
    /// </summary>
    TwoFactorLocked,

    /// <summary>
    /// Selects the two factor challenge expired option.
    /// </summary>
    TwoFactorChallengeExpired,

    /// <summary>
    /// Selects the two factor resend not allowed option.
    /// </summary>
    TwoFactorResendNotAllowed,

    /// <summary>
    /// Selects the two factor resend cooldown active option.
    /// </summary>
    TwoFactorResendCooldownActive,

    /// <summary>
    /// Selects the authenticator setup expired option.
    /// </summary>
    AuthenticatorSetupExpired,

    /// <summary>
    /// Selects the authenticator not enabled option.
    /// </summary>
    AuthenticatorNotEnabled,

    /// <summary>
    /// Selects the authenticator already enabled option.
    /// </summary>
    AuthenticatorAlreadyEnabled,

    /// <summary>
    /// Selects the email no recipients option.
    /// </summary>
    EmailNoRecipients,

    /// <summary>
    /// Selects the email recipient without address option.
    /// </summary>
    EmailRecipientWithoutAddress,

    /// <summary>
    /// A promotional email was written to someone without promotional consent.
    /// </summary>
    EmailRecipientWithoutConsent,

    /// <summary>
    /// The unsubscribe link of a promotional email was altered or not issued here.
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
    /// Selects the authentication required option.
    /// </summary>
    AuthenticationRequired,

    /// <summary>
    /// Selects the endpoint not found option.
    /// </summary>
    EndpointNotFound,

    /// <summary>
    /// Selects the access denied option.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// Selects the invalid csrf token option.
    /// </summary>
    InvalidCsrfToken,

    /// <summary>
    /// The client sent more requests than its rate or concurrency budget allows; it may retry after
    /// the seconds in <c>Retry-After</c>.
    /// </summary>
    TooManyRequests,

    /// <summary>
    /// Selects the request validation failed option.
    /// </summary>
    RequestValidationFailed,

    /// <summary>
    /// Another request changed or removed the data the operation read before it could store its
    /// change, so the change of this request was refused and the request can be sent again.
    /// </summary>
    ConcurrentUpdate,

    /// <summary>
    /// Selects the unexpected error option.
    /// </summary>
    UnexpectedError,
}
