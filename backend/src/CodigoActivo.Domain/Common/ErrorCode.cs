using System.Text.Json.Serialization;

namespace CodigoActivo.Domain.Common;

/// <summary>
/// Identifies the supported error code values.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ErrorCode>))]
public enum ErrorCode
{
    /// <summary>
    /// Selects the announcement not found option.
    /// </summary>
    AnnouncementNotFound,
    /// <summary>
    /// Selects the announcement thumbnail not found option.
    /// </summary>
    AnnouncementThumbnailNotFound,
    /// <summary>
    /// Selects the activity not found option.
    /// </summary>
    ActivityNotFound,
    /// <summary>
    /// Selects the activity modality type not found option.
    /// </summary>
    ActivityModalityTypeNotFound,
    /// <summary>
    /// Selects the activity signup closed option.
    /// </summary>
    ActivitySignupClosed,
    /// <summary>
    /// Selects the activity signup early only option.
    /// </summary>
    ActivitySignupEarlyOnly,
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
    /// Selects the assignment status type not found option.
    /// </summary>
    AssignmentStatusTypeNotFound,
    /// <summary>
    /// Selects the activity role type not found option.
    /// </summary>
    ActivityRoleTypeNotFound,
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
    /// Selects the event rating not finished option.
    /// </summary>
    EventRatingNotFinished,
    /// <summary>
    /// Selects the event rating attendance required option.
    /// </summary>
    EventRatingAttendanceRequired,
    /// <summary>
    /// Selects the event rating already submitted option.
    /// </summary>
    EventRatingAlreadySubmitted,
    /// <summary>
    /// Selects the event terms acceptance required option.
    /// </summary>
    EventTermsAcceptanceRequired,
    /// <summary>
    /// Selects the event terms document duplicated option.
    /// </summary>
    EventTermsDocumentDuplicated,
    /// <summary>
    /// Selects the terms document not found option.
    /// </summary>
    TermsDocumentNotFound,
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
    /// Selects the resource type not found option.
    /// </summary>
    ResourceTypeNotFound,
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
    /// Selects the user delete admin forbidden option.
    /// </summary>
    UserDeleteAdminForbidden,
    /// <summary>
    /// Selects the user self delete requires verification option.
    /// </summary>
    UserSelfDeleteRequiresVerification,
    /// <summary>
    /// Selects the user delete authored content exists option.
    /// </summary>
    UserDeleteAuthoredContentExists,
    /// <summary>
    /// Selects the user cannot remove last admin option.
    /// </summary>
    UserCannotRemoveLastAdmin,
    /// <summary>
    /// Selects the user type not found option.
    /// </summary>
    UserTypeNotFound,
    /// <summary>
    /// Selects the parent user not found option.
    /// </summary>
    ParentUserNotFound,
    /// <summary>
    /// Selects the user parent is minor option.
    /// </summary>
    UserParentIsMinor,
    /// <summary>
    /// Selects the user child birth date not minor option.
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
    /// Selects the user parent identifier required option.
    /// </summary>
    UserParentIdRequired,
    /// <summary>
    /// Selects the user cannot be own parent option.
    /// </summary>
    UserCannotBeOwnParent,
    /// <summary>
    /// Selects the user parent reassignment forbidden option.
    /// </summary>
    UserParentReassignmentForbidden,
    /// <summary>
    /// Selects the user parent not allowed for adult option.
    /// </summary>
    UserParentNotAllowedForAdult,
    /// <summary>
    /// Selects the user cannot become a dependent minor option.
    /// </summary>
    UserCannotBecomeMinor,
    /// <summary>
    /// Selects the user contact info required option.
    /// </summary>
    UserContactInfoRequired,
    /// <summary>
    /// Selects the user email already in use option.
    /// </summary>
    UserEmailAlreadyInUse,
    /// <summary>
    /// Selects the user phone already in use option.
    /// </summary>
    UserPhoneAlreadyInUse,
    /// <summary>
    /// Selects the invalid credentials option.
    /// </summary>
    InvalidCredentials,
    /// <summary>
    /// Selects the user account blocked option.
    /// </summary>
    UserAccountBlocked,
    /// <summary>
    /// Selects the user account is dependent option.
    /// </summary>
    UserAccountIsDependent,
    /// <summary>
    /// Selects the user account pending verification option.
    /// </summary>
    UserAccountPendingVerification,
    /// <summary>
    /// Selects the current user not found option.
    /// </summary>
    CurrentUserNotFound,
    /// <summary>
    /// Selects the register adult cannot be minor option.
    /// </summary>
    RegisterAdultCannotBeMinor,
    /// <summary>
    /// Selects the register contact info required option.
    /// </summary>
    RegisterContactInfoRequired,
    /// <summary>
    /// Selects the register email or phone already in use option.
    /// </summary>
    RegisterEmailOrPhoneAlreadyInUse,
    /// <summary>
    /// Selects the register minor birth date not minor option.
    /// </summary>
    RegisterMinorBirthDateNotMinor,
    /// <summary>
    /// Selects the otp invalid or expired option.
    /// </summary>
    OtpInvalidOrExpired,
    /// <summary>
    /// Selects the otp resend not allowed option.
    /// </summary>
    OtpResendNotAllowed,
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
    /// Selects the email no recipients option.
    /// </summary>
    EmailNoRecipients,
    /// <summary>
    /// Selects the email recipient without address option.
    /// </summary>
    EmailRecipientWithoutAddress,
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
    /// Selects the access denied option.
    /// </summary>
    AccessDenied,
    /// <summary>
    /// Selects the invalid csrf token option.
    /// </summary>
    InvalidCsrfToken,
    /// <summary>
    /// Selects the request validation failed option.
    /// </summary>
    RequestValidationFailed,
    /// <summary>
    /// Selects the unexpected error option.
    /// </summary>
    UnexpectedError,
}
