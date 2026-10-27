/** Who ran an audited command. */
export type AuditActorType = 'User' | 'Anonymous' | 'System'

/** Command an audit entry records, named after it. */
export type AuditAction =
  | 'BeginAuthenticatorSetup'
  | 'ChangePassword'
  | 'CompleteLogin'
  | 'ConfirmAuthenticator'
  | 'ConfirmEmailChange'
  | 'DeleteOwnAccount'
  | 'DisableAuthenticator'
  | 'EndLoginChallenge'
  | 'EndSession'
  | 'ForgotPassword'
  | 'Login'
  | 'Register'
  | 'RemoveExpiredSessions'
  | 'PurgeUnverifiedAccounts'
  | 'PurgeUnusedFiles'
  | 'RequestAccountDeletionCode'
  | 'ResendTwoFactorCode'
  | 'ResendVerification'
  | 'ResetPassword'
  | 'VerifyUser'
  | 'AssignActivity'
  | 'AssignHousehold'
  | 'ChangeAssignmentRole'
  | 'ChangeAssignmentStatus'
  | 'CreateActivity'
  | 'DeleteActivity'
  | 'UnassignActivity'
  | 'UpdateActivity'
  | 'SendEmailToEventAttendees'
  | 'SendEmailToUser'
  | 'SendEmailToUsers'
  | 'Unsubscribe'
  | 'CreateEventCategoryType'
  | 'DeleteEventCategoryType'
  | 'UpdateEventCategoryType'
  | 'CreateEvent'
  | 'DeleteEvent'
  | 'SetEventFeatured'
  | 'UpdateEvent'
  | 'WithdrawTermsConsent'
  | 'CreateFile'
  | 'DeleteFile'
  | 'CreateNewsItem'
  | 'DeleteNewsItem'
  | 'SetNewsItemFeatured'
  | 'UpdateNewsItem'
  | 'CreatePartner'
  | 'DeletePartner'
  | 'UpdatePartner'
  | 'CreateResource'
  | 'DeleteResource'
  | 'UpdateResource'
  | 'CreateTermsDocument'
  | 'DeleteTermsDocument'
  | 'UpdateTermsDocument'
  | 'PublishLegalPolicy'
  | 'AddChild'
  | 'ChangeUserType'
  | 'DeleteUser'
  | 'PurgeAdultDependents'
  | 'ResetTwoFactor'
  | 'SendComingOfAgeNotices'
  | 'SetAdmin'
  | 'UpdateUser'
  | 'PurgeDataHistory'
  | 'RequestBlockedDataConsultationCode'
  | 'OpenBlockedDataConsultation'
  | 'SearchBlockedData'
  | 'ExportBlockedData'
  | 'SendConsentWithdrawalNotices'
  | 'SendBlockedDataConsultationNotices'

/** Kind of thing an audit entry points at. */
export type AuditEntityType =
  | 'Activity'
  | 'BlockedDataConsultation'
  | 'Event'
  | 'EventCategoryType'
  | 'LegalPolicy'
  | 'NewsItem'
  | 'Partner'
  | 'Resource'
  | 'StoredFile'
  | 'TermsDocument'
  | 'User'

/** How an audited command ended. */
export type AuditOutcome = 'Succeeded' | 'Rejected' | 'Failed'

/**
 * One thing an audited command named: a person's full name or a title, empty when it no longer
 * exists or is a consultation of the blocked data, which the audit log never names.
 */
export interface AuditEntityRef {
  readonly type: AuditEntityType
  readonly id: string
  readonly name: string
}

/**
 * One audited command. Text fields are empty when unknown: an actor with an id but no name is an
 * account erased since.
 */
export interface AuditEntry {
  readonly id: string
  readonly occurredAt: string
  readonly actorType: AuditActorType
  readonly actorId: string
  readonly actorName: string
  readonly actorEmail: string
  readonly clientIp: string
  readonly action: AuditAction
  readonly entity: AuditEntityRef | null
  readonly related: readonly AuditEntityRef[]
  readonly outcome: AuditOutcome
  readonly errorCode: string
  readonly traceId: string
}

/** Filters, sort and page of the audit list; an unset filter keeps every entry. */
export interface AuditListParams {
  readonly search?: string
  readonly actorType?: AuditActorType
  readonly actorId?: string
  readonly action?: AuditAction
  readonly entityType?: AuditEntityType
  readonly entityId?: string
  readonly outcome?: AuditOutcome
  readonly clientIp?: string
  readonly traceId?: string
  readonly occurredFrom?: string
  readonly occurredTo?: string
  readonly page?: number
  readonly pageSize?: number
  readonly sort?: string
}
