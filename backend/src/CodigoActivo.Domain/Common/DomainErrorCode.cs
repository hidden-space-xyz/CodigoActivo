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
    /// The signup window of the event is closed.
    /// </summary>
    ActivitySignupClosed,

    /// <summary>
    /// Only early signup is open and someone signing up is not entitled to it.
    /// </summary>
    ActivitySignupEarlyOnly,

    /// <summary>
    /// The activity has already started, so nobody can sign up for it or withdraw from it.
    /// </summary>
    ActivityAlreadyStarted,

    /// <summary>
    /// The type of the person does not allow the role asked for.
    /// </summary>
    ActivityRoleNotAllowed,

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
    /// An event links a consent as required, while declining a consent never stops a signup.
    /// </summary>
    EventTermsConsentRequired,

    /// <summary>
    /// A consent was to be withdrawn while it is not given now.
    /// </summary>
    EventTermsConsentNotGiven,

    /// <summary>
    /// A document that holds conditions was to be withdrawn; only a consent can be withdrawn.
    /// </summary>
    EventTermsWithdrawalNotAllowed,

    /// <summary>
    /// A terms document changed after the person read it; the decision has to be taken again on the
    /// current version.
    /// </summary>
    EventTermsDocumentChanged,

    /// <summary>
    /// A terms document that asks for a signature was accepted without one.
    /// </summary>
    EventTermsSignatureRequired,

    /// <summary>
    /// A signature is not a PNG image within the size limits.
    /// </summary>
    EventTermsSignatureInvalid,

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
    /// Selects the option for administrator rights given to a dependent.
    /// </summary>
    UserDependentCannotBeAdmin,

    /// <summary>
    /// Selects the option for administrator rights given to an account whose email is unconfirmed.
    /// </summary>
    UserPendingVerificationCannotBeAdmin,

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
    /// Selects the option for a registration whose holder did not declare being of age.
    /// </summary>
    UserAdultDeclarationRequired,

    /// <summary>
    /// Selects the option for a dependent added by someone who did not declare being its parent or
    /// legal guardian.
    /// </summary>
    UserGuardianshipDeclarationRequired,

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

    /// <summary>
    /// The account no longer waits for its email to be verified.
    /// </summary>
    UserNotPendingVerification,

    /// <summary>
    /// Too many wrong second-factor codes locked the second factor for a while.
    /// </summary>
    TwoFactorLocked,

    /// <summary>
    /// The account does not use email as its second factor, so no code can be emailed to it.
    /// </summary>
    TwoFactorResendNotAllowed,

    /// <summary>
    /// No authenticator enrollment waits for confirmation, or the one that did has expired.
    /// </summary>
    AuthenticatorSetupExpired,

    /// <summary>
    /// A role of an activity asks for fewer or more people than a role can take.
    /// </summary>
    ActivityRoleCapacityOutOfRange,

    /// <summary>
    /// The score of a rating lies outside the allowed scale.
    /// </summary>
    EventRatingScoreOutOfRange,

    /// <summary>
    /// An answer of a rating is longer than allowed.
    /// </summary>
    EventRatingAnswerTooLong,

    /// <summary>
    /// The category is the only one of an event, which must keep at least one.
    /// </summary>
    EventCategoryTypeOnlyCategoryOfEvent,

    /// <summary>
    /// A legal policy was to be published without any text.
    /// </summary>
    LegalPolicyContentRequired,
}
