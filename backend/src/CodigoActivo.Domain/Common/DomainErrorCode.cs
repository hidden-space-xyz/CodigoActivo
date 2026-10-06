namespace CodigoActivo.Domain.Common;

/// <summary>
/// Identifies the business rules an aggregate or a domain policy can reject.
/// </summary>
public enum DomainErrorCode
{
    /// <summary>
    /// Selects the activity assignment already exists option.
    /// </summary>
    ActivityAssignmentAlreadyExists,

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
    /// Selects the activity role capacity duplicated option.
    /// </summary>
    ActivityRoleCapacityDuplicated,

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
    /// Selects the event rating empty option.
    /// </summary>
    EventRatingEmpty,

    /// <summary>
    /// Selects the event terms document duplicated option.
    /// </summary>
    EventTermsDocumentDuplicated,

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
    /// Selects the user delete initial admin forbidden option.
    /// </summary>
    UserDeleteInitialAdminForbidden,

    /// <summary>
    /// Selects the user cannot remove initial admin option.
    /// </summary>
    UserCannotRemoveInitialAdmin,

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
    /// Selects the option for a secondary phone equal to the main phone of the same account.
    /// </summary>
    SecondaryPhoneSameAsPrimary,

    /// <summary>
    /// Selects the user phone invalid option.
    /// </summary>
    UserPhoneInvalid,

    /// <summary>
    /// Selects the authenticator already enabled option.
    /// </summary>
    AuthenticatorAlreadyEnabled,

    /// <summary>
    /// The DNI or NIE of an independent account has a control letter that does not match its number.
    /// </summary>
    UserNationalIdInvalid,

    /// <summary>
    /// The email of an independent account is malformed.
    /// </summary>
    UserEmailInvalid,

    /// <summary>
    /// The new birth date of a dependent lies in the future.
    /// </summary>
    UserChildBirthDateInFuture,
}
