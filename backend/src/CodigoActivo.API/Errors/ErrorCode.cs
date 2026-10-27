namespace CodigoActivo.API.Errors;

/// <summary>
/// Identifies every failure the API reports in <c>ApiErrorResponse.Code</c>. It is the wire contract
/// the frontend translates, so members are never renamed or reordered.
/// </summary>
public enum ErrorCode
{
    /// <summary>
    /// No news item has the requested identifier.
    /// </summary>
    NewsItemNotFound,

    /// <summary>
    /// The thumbnail file chosen for a news item does not exist.
    /// </summary>
    NewsItemThumbnailNotFound,

    /// <summary>
    /// No activity has the requested identifier.
    /// </summary>
    ActivityNotFound,

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
    /// The person is already signed up to the activity.
    /// </summary>
    ActivityAssignmentAlreadyExists,

    /// <summary>
    /// A household signup listed no member.
    /// </summary>
    ActivityHouseholdAssignmentsRequired,

    /// <summary>
    /// A member of a household signup does not exist or is outside the household of the signed-in
    /// user.
    /// </summary>
    ActivityHouseholdMemberNotAllowed,

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
    /// An activity falls outside the days of its event.
    /// </summary>
    ActivityScheduleOutsideEventRange,

    /// <summary>
    /// The thumbnail file chosen for an activity does not exist.
    /// </summary>
    ActivityThumbnailNotFound,

    /// <summary>
    /// A role appears more than once among the capacities of an activity.
    /// </summary>
    ActivityRoleCapacityDuplicated,

    /// <summary>
    /// No event has the requested identifier.
    /// </summary>
    EventNotFound,

    /// <summary>
    /// The new days of an event would leave one of its activities outside them.
    /// </summary>
    EventActivitiesOutsideNewRange,

    /// <summary>
    /// No event category type has the requested identifier.
    /// </summary>
    EventCategoryTypeNotFound,

    /// <summary>
    /// Another event category type already has that name.
    /// </summary>
    EventCategoryTypeNameAlreadyExists,

    /// <summary>
    /// The category is the only one of an event, which must keep at least one.
    /// </summary>
    EventCategoryTypeOnlyCategoryOfEvent,

    /// <summary>
    /// The thumbnail file chosen for an event does not exist.
    /// </summary>
    EventThumbnailNotFound,

    /// <summary>
    /// A file embedded in the rich text of content no longer exists.
    /// </summary>
    RichTextFileNotFound,

    /// <summary>
    /// An event was saved without any category.
    /// </summary>
    EventCategoriesRequired,

    /// <summary>
    /// A date of an event other than the early signup is missing.
    /// </summary>
    EventScheduleRequired,

    /// <summary>
    /// An event ends before it starts, or its signup does not close after it opens or opens after
    /// the last day of the event.
    /// </summary>
    EventScheduleInvalidRange,

    /// <summary>
    /// The early signup of an event does not open before the regular one.
    /// </summary>
    EventEarlySignupNotBeforeSignup,

    /// <summary>
    /// The signup of an event closes after the last day of the event.
    /// </summary>
    EventSignupEndsAfterEvent,

    /// <summary>
    /// An event was rated before it ended.
    /// </summary>
    EventRatingNotFinished,

    /// <summary>
    /// An event was rated by someone with no confirmed signup in their household.
    /// </summary>
    EventRatingAttendanceRequired,

    /// <summary>
    /// A rating carries neither a score nor any answer.
    /// </summary>
    EventRatingEmpty,

    /// <summary>
    /// The signed-in account has not rated the event.
    /// </summary>
    EventRatingNotFound,

    /// <summary>
    /// A signup left a required terms document of the event unaccepted.
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
    /// No decision was taken about that document of the event for that person.
    /// </summary>
    EventTermsDecisionNotFound,

    /// <summary>
    /// An event links the same terms document twice.
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
    /// No terms document has the requested identifier.
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
    /// A legal policy was to be published without any text.
    /// </summary>
    LegalPolicyContentRequired,

    /// <summary>
    /// No policy of that kind was published yet.
    /// </summary>
    LegalPolicyNotFound,

    /// <summary>
    /// The policy has no version with that number.
    /// </summary>
    LegalPolicyVersionNotFound,

    /// <summary>
    /// Another administrator published the first version of the same policy at the same time; the
    /// change can be sent again.
    /// </summary>
    LegalPolicyPublishedConcurrently,

    /// <summary>
    /// No resource has the requested identifier.
    /// </summary>
    ResourceNotFound,

    /// <summary>
    /// The thumbnail file chosen for a resource does not exist.
    /// </summary>
    ResourceThumbnailNotFound,

    /// <summary>
    /// A resource hosted on the site has an empty body.
    /// </summary>
    ResourceDescriptionRequired,

    /// <summary>
    /// An external resource has a body; it only links elsewhere.
    /// </summary>
    ResourceDescriptionNotAllowed,

    /// <summary>
    /// An external resource has no link.
    /// </summary>
    ResourceUrlRequired,

    /// <summary>
    /// A resource hosted on the site has a link.
    /// </summary>
    ResourceUrlNotAllowed,

    /// <summary>
    /// No partner has the requested identifier.
    /// </summary>
    PartnerNotFound,

    /// <summary>
    /// The thumbnail file chosen for a partner does not exist.
    /// </summary>
    PartnerThumbnailNotFound,

    /// <summary>
    /// No file has the requested identifier.
    /// </summary>
    FileNotFound,

    /// <summary>
    /// A file cannot be deleted while any content references it.
    /// </summary>
    FileInUse,

    /// <summary>
    /// The file is known but its stored content is missing.
    /// </summary>
    FileContentMissingFromStorage,

    /// <summary>
    /// An upload carries no file.
    /// </summary>
    FileUploadMissing,

    /// <summary>
    /// An uploaded file is empty.
    /// </summary>
    FileUploadEmpty,

    /// <summary>
    /// An uploaded file is larger than the upload limit.
    /// </summary>
    FileUploadTooLarge,

    /// <summary>
    /// The content of an upload cannot be read more than once.
    /// </summary>
    FileUploadStreamNotSeekable,

    /// <summary>
    /// An uploaded file is not a JPEG, PNG or WebP image.
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
    /// No account has the requested identifier.
    /// </summary>
    UserNotFound,

    /// <summary>
    /// A user tried to delete their own account through the endpoint for others; their own
    /// deletion asks for their password and second factor.
    /// </summary>
    UserSelfDeleteRequiresVerification,

    /// <summary>
    /// A user tried to grant or revoke their own administrator rights.
    /// </summary>
    UserSelfAdminChangeForbidden,

    /// <summary>
    /// A user tried to change their own membership type.
    /// </summary>
    UserSelfTypeChangeForbidden,

    /// <summary>
    /// The initial administrator can never be deleted.
    /// </summary>
    UserDeleteInitialAdminForbidden,

    /// <summary>
    /// The initial administrator can never lose its administrator rights.
    /// </summary>
    UserCannotRemoveInitialAdmin,

    /// <summary>
    /// Administrator rights were given to a dependent.
    /// </summary>
    UserDependentCannotBeAdmin,

    /// <summary>
    /// Administrator rights were given to an account whose email is unconfirmed.
    /// </summary>
    UserPendingVerificationCannotBeAdmin,

    /// <summary>
    /// The guardian a dependent is added to does not exist.
    /// </summary>
    ParentUserNotFound,

    /// <summary>
    /// The guardian of a dependent is itself a dependent.
    /// </summary>
    UserParentIsMinor,

    /// <summary>
    /// A dependent was created, when registering or later, or given a new birth date that does not
    /// make it a minor.
    /// </summary>
    UserChildBirthDateNotMinor,

    /// <summary>
    /// The password of an account without one was to be changed.
    /// </summary>
    UserPasswordNotSet,

    /// <summary>
    /// The current password re-entered to authorize a change is wrong.
    /// </summary>
    UserCurrentPasswordIncorrect,

    /// <summary>
    /// A new password equals the current one.
    /// </summary>
    UserNewPasswordSameAsCurrent,

    /// <summary>
    /// A dependent was to be moved to another guardian.
    /// </summary>
    UserParentReassignmentForbidden,

    /// <summary>
    /// The guardian already has the most dependents a household may hold.
    /// </summary>
    UserChildLimitReached,

    /// <summary>
    /// A guardian was given to an account that is not a dependent.
    /// </summary>
    UserParentNotAllowedForAdult,

    /// <summary>
    /// A birth date was given to an account that is not a dependent.
    /// </summary>
    UserBirthDateNotAllowedForAdult,

    /// <summary>
    /// A dependent was updated without a birth date.
    /// </summary>
    UserChildBirthDateRequired,

    /// <summary>
    /// An independent account was registered or updated without an email or a phone, or asked for
    /// an emailed code while it has no email.
    /// </summary>
    UserContactInfoRequired,

    /// <summary>
    /// An independent account was registered or updated without a DNI or NIE.
    /// </summary>
    UserNationalIdRequired,

    /// <summary>
    /// A registration whose holder did not declare being of age.
    /// </summary>
    UserAdultDeclarationRequired,

    /// <summary>
    /// A dependent was added by someone who did not declare being its parent or legal guardian.
    /// </summary>
    UserGuardianshipDeclarationRequired,

    /// <summary>
    /// An email change was confirmed for an address another account already owns.
    /// </summary>
    UserEmailAlreadyInUse,

    /// <summary>
    /// The secondary phone of an account equals its main phone.
    /// </summary>
    SecondaryPhoneSameAsPrimary,

    /// <summary>
    /// A phone number does not have the shape of a phone number.
    /// </summary>
    UserPhoneInvalid,

    /// <summary>
    /// An email address whose domain is on the disposable email domain list, refused when
    /// registering or changing an account email.
    /// </summary>
    DisposableEmailNotAllowed,

    /// <summary>
    /// The password step of a login failed: unknown email, wrong password, or an account without a
    /// password or locked by wrong passwords, all answered alike.
    /// </summary>
    InvalidCredentials,

    /// <summary>
    /// The account is blocked and cannot sign in.
    /// </summary>
    UserAccountBlocked,

    /// <summary>
    /// The account has not verified its email yet and cannot sign in.
    /// </summary>
    UserAccountPendingVerification,

    /// <summary>
    /// The account a request has just verified or signed in no longer exists.
    /// </summary>
    CurrentUserNotFound,

    /// <summary>
    /// The code of an account verification or email change link is wrong or expired, or no such
    /// verification or change waits.
    /// </summary>
    OtpInvalidOrExpired,

    /// <summary>
    /// The email limit refused a new email change link; it can be asked for again later.
    /// </summary>
    OtpResendCooldownActive,

    /// <summary>
    /// The code of a password reset link is wrong or expired.
    /// </summary>
    PasswordResetInvalidOrExpired,

    /// <summary>
    /// A second-factor code is wrong; wrong codes count towards the lockout.
    /// </summary>
    TwoFactorCodeInvalid,

    /// <summary>
    /// Too many wrong second-factor codes locked the second factor for a while.
    /// </summary>
    TwoFactorLocked,

    /// <summary>
    /// No second-factor challenge is open for the caller, or the one that was has expired.
    /// </summary>
    TwoFactorChallengeExpired,

    /// <summary>
    /// The account does not use email as its second factor, so no code can be emailed to it.
    /// </summary>
    TwoFactorResendNotAllowed,

    /// <summary>
    /// A new emailed code was asked for within the resend cooldown or refused by the email limit.
    /// </summary>
    TwoFactorResendCooldownActive,

    /// <summary>
    /// No authenticator enrollment waits for confirmation, or the one that did has expired.
    /// </summary>
    AuthenticatorSetupExpired,

    /// <summary>
    /// The account does not use an authenticator application as its second factor.
    /// </summary>
    AuthenticatorNotEnabled,

    /// <summary>
    /// The account already uses an authenticator application as its second factor.
    /// </summary>
    AuthenticatorAlreadyEnabled,

    /// <summary>
    /// A manual email would reach nobody.
    /// </summary>
    EmailNoRecipients,

    /// <summary>
    /// A manual email was written to an account without an email address.
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
    /// A manual email would reach more recipients than one dispatch allows.
    /// </summary>
    EmailTooManyRecipients,

    /// <summary>
    /// A manual email carries more attachments than allowed.
    /// </summary>
    EmailTooManyAttachments,

    /// <summary>
    /// An attachment of a manual email is empty.
    /// </summary>
    EmailAttachmentEmpty,

    /// <summary>
    /// The attachments of a manual email are larger in total than allowed.
    /// </summary>
    EmailAttachmentsTooLarge,

    /// <summary>
    /// An email could not be queued for delivery, for instance because the outbox is full.
    /// </summary>
    EmailSendFailed,

    /// <summary>
    /// The endpoint needs a signed-in user and the request has no valid session.
    /// </summary>
    AuthenticationRequired,

    /// <summary>
    /// No endpoint answers the requested <c>/api</c> path.
    /// </summary>
    EndpointNotFound,

    /// <summary>
    /// The signed-in user may not use the endpoint or act for the person the request names.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// An unsafe request carries no antiforgery token or a wrong one; the client fetches a new one
    /// and sends the request again.
    /// </summary>
    InvalidCsrfToken,

    /// <summary>
    /// The client sent more requests than its rate or concurrency budget allows; it may retry after
    /// the seconds in <c>Retry-After</c>.
    /// </summary>
    TooManyRequests,

    /// <summary>
    /// The request is malformed or breaks a rule of its fields, such as a missing or too long value.
    /// </summary>
    RequestValidationFailed,

    /// <summary>
    /// Another request changed or removed the data the operation read before it could store its
    /// change, so the change of this request was refused and the request can be sent again.
    /// </summary>
    ConcurrentUpdate,

    /// <summary>
    /// The request failed for a reason the response does not disclose; the failure is logged.
    /// </summary>
    UnexpectedError,

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

    /// <summary>
    /// Only the administrator designated for data protection may consult the blocked data.
    /// </summary>
    BlockedDataOfficerOnly,
}
