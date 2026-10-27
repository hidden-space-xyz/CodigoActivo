namespace CodigoActivo.Domain.Common;

/// <summary>
/// Identifies the business rules an aggregate or a domain policy can reject.
/// </summary>
public enum DomainErrorCode
{
    /// <summary>
    /// The person is already signed up to the activity.
    /// </summary>
    ActivityAssignmentAlreadyExists,

    /// <summary>
    /// The person is not signed up to the activity.
    /// </summary>
    ActivityAssignmentNotFound,

    /// <summary>
    /// The start or the end of an activity is missing.
    /// </summary>
    ActivityScheduleRequired,

    /// <summary>
    /// An activity does not end after it starts.
    /// </summary>
    ActivityScheduleInvalidRange,

    /// <summary>
    /// An activity starts or ends outside the days of its event.
    /// </summary>
    ActivityScheduleOutsideEventRange,

    /// <summary>
    /// A role appears more than once among the capacities of an activity.
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
    /// The activity has already started, so only an administrator can still sign people up for it or
    /// withdraw them from it.
    /// </summary>
    ActivityAlreadyStarted,

    /// <summary>
    /// The type of the person does not allow the role asked for.
    /// </summary>
    ActivityRoleNotAllowed,

    /// <summary>
    /// An event was given no category.
    /// </summary>
    EventCategoriesRequired,

    /// <summary>
    /// A day of an event, or the opening or closing of its signup, is missing.
    /// </summary>
    EventScheduleRequired,

    /// <summary>
    /// An event ends before it starts, its signup does not close after it opens, or its signup
    /// opens after the last day of the event.
    /// </summary>
    EventScheduleInvalidRange,

    /// <summary>
    /// The early signup of an event does not open before the regular signup.
    /// </summary>
    EventEarlySignupNotBeforeSignup,

    /// <summary>
    /// The signup of an event closes after the last day of the event.
    /// </summary>
    EventSignupEndsAfterEvent,

    /// <summary>
    /// A rating has neither a score nor any answer.
    /// </summary>
    EventRatingEmpty,

    /// <summary>
    /// A terms document is linked to an event more than once.
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
    /// A resource hosted on the site has an empty body.
    /// </summary>
    ResourceDescriptionRequired,

    /// <summary>
    /// An external resource has a body; it holds only a link.
    /// </summary>
    ResourceDescriptionNotAllowed,

    /// <summary>
    /// An external resource has no link.
    /// </summary>
    ResourceUrlRequired,

    /// <summary>
    /// A resource hosted on the site has a link; it holds only a body.
    /// </summary>
    ResourceUrlNotAllowed,

    /// <summary>
    /// The initial administrator was to be deleted.
    /// </summary>
    UserDeleteInitialAdminForbidden,

    /// <summary>
    /// The initial administrator was to lose its administrator rights.
    /// </summary>
    UserCannotRemoveInitialAdmin,

    /// <summary>
    /// Administrator rights were to be given to a dependent.
    /// </summary>
    UserDependentCannotBeAdmin,

    /// <summary>
    /// Administrator rights were to be given to an account whose email is unconfirmed.
    /// </summary>
    UserPendingVerificationCannotBeAdmin,

    /// <summary>
    /// A dependent was to be added under an account that is itself a dependent.
    /// </summary>
    UserParentIsMinor,

    /// <summary>
    /// A dependent was created, when registering or later, or given a new birth date that does not
    /// make it a minor.
    /// </summary>
    UserChildBirthDateNotMinor,

    /// <summary>
    /// A change to a dependent names a guardian other than the one it has.
    /// </summary>
    UserParentReassignmentForbidden,

    /// <summary>
    /// The guardian already has the most dependents a household allows.
    /// </summary>
    UserChildLimitReached,

    /// <summary>
    /// A change to an independent account names a guardian.
    /// </summary>
    UserParentNotAllowedForAdult,

    /// <summary>
    /// A birth date was given to an account that is not a dependent.
    /// </summary>
    UserBirthDateNotAllowedForAdult,

    /// <summary>
    /// A dependent was created or updated without a birth date.
    /// </summary>
    UserChildBirthDateRequired,

    /// <summary>
    /// An independent account was registered or updated without an email or a phone, or an emailed
    /// code was asked for an account that has no email.
    /// </summary>
    UserContactInfoRequired,

    /// <summary>
    /// An independent account was registered or updated without a DNI or NIE.
    /// </summary>
    UserNationalIdRequired,

    /// <summary>
    /// An account was registered without its holder declaring being of age.
    /// </summary>
    UserAdultDeclarationRequired,

    /// <summary>
    /// A dependent was added by someone who did not declare being its parent or legal guardian.
    /// </summary>
    UserGuardianshipDeclarationRequired,

    /// <summary>
    /// The secondary phone of an account equals its main phone.
    /// </summary>
    SecondaryPhoneSameAsPrimary,

    /// <summary>
    /// A phone does not have 7 to 15 digits, or has characters other than spaces, dots, hyphens,
    /// parentheses and a leading plus sign.
    /// </summary>
    UserPhoneInvalid,

    /// <summary>
    /// An authenticator was to be enrolled or enabled for an account that already uses one.
    /// </summary>
    AuthenticatorAlreadyEnabled,

    /// <summary>
    /// The DNI or NIE of an independent account is not shaped as one or has a control letter that
    /// does not match its number.
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
    /// A legal policy was to be published without any text or image.
    /// </summary>
    LegalPolicyContentRequired,

    /// <summary>
    /// A consultation of the blocked data was to be opened without the authority or the
    /// reference of the request.
    /// </summary>
    BlockedDataReferenceRequired,

    /// <summary>
    /// The consultation of the blocked data does not exist or another account opened it.
    /// </summary>
    BlockedDataConsultationNotFound,

    /// <summary>
    /// The consultation of the blocked data is over; a new one must be opened.
    /// </summary>
    BlockedDataConsultationExpired,

    /// <summary>
    /// No search of the consultation found the account.
    /// </summary>
    BlockedDataAccountNotFound,
}
