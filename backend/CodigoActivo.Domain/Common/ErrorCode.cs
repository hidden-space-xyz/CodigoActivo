using System.Text.Json.Serialization;

namespace CodigoActivo.Domain.Common;

[JsonConverter(typeof(JsonStringEnumConverter<ErrorCode>))]
public enum ErrorCode
{
    // Announcement
    AnnouncementNotFound,
    AnnouncementThumbnailNotFound,

    // Activity
    ActivityNotFound,
    ActivityModalityTypeNotFound,
    ActivitySignupClosed,
    ActivityRoleNotAllowed,
    ActivityAssignmentAlreadyExists,
    ActivityHouseholdAssignmentsRequired,
    ActivityHouseholdMemberNotAllowed,
    ActivityAssignmentNotFound,
    AssignmentStatusTypeNotFound,
    ActivityRoleTypeNotFound,
    ActivityScheduleRequired,
    ActivityScheduleInvalidRange,
    ActivityScheduleOutsideEventRange,
    ActivityThumbnailNotFound,

    // Event
    EventNotFound,
    EventActivitiesOutsideNewRange,
    EventCategoryTypeNotFound,
    EventThumbnailNotFound,
    EventCategoriesRequired,
    EventScheduleRequired,
    EventScheduleInvalidRange,

    // Resource
    ResourceNotFound,
    ResourceThumbnailNotFound,

    // Partner
    PartnerNotFound,
    PartnerThumbnailNotFound,

    // File
    FileNotFound,
    FileContentMissingFromStorage,
    FileUploadMissing,
    FileUploadEmpty,
    FileUploadTooLarge,
    FileUploadStreamNotSeekable,
    FileUploadUnsupportedFormat,

    // User
    UserNotFound,
    UserDeleteAdminForbidden,
    UserTypeNotFound,
    ParentUserNotFound,
    UserParentIsMinor,
    UserChildBirthDateNotMinor,
    UserTypeNotAllowedForMinors,
    UserPasswordNotSet,
    UserCurrentPasswordIncorrect,
    UserParentIdRequired,
    UserCannotBeOwnParent,
    UserParentNotAllowedForAdult,
    UserContactInfoRequired,
    UserEmailAlreadyInUse,
    UserPhoneAlreadyInUse,

    // Auth
    InvalidCredentials,
    UserAccountBlocked,
    UserAccountIsDependent,
    UserAccountPendingVerification,
    CurrentUserNotFound,
    RegisterAdultCannotBeMinor,
    UserTypeNotAllowedForAdults,
    RegisterContactInfoRequired,
    RegisterEmailOrPhoneAlreadyInUse,
    RegisterMinorBirthDateNotMinor,
    OtpInvalidOrExpired,

    // Infrastructure (auth pipeline, CSRF, model validation, unhandled exceptions)
    AuthenticationRequired,
    AccessDenied,
    InvalidCsrfToken,
    RequestValidationFailed,
    UnexpectedError
}