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
  | 'CreateEventCategoryType'
  | 'DeleteEventCategoryType'
  | 'UpdateEventCategoryType'
  | 'CreateEvent'
  | 'DeleteEvent'
  | 'SetEventFeatured'
  | 'UpdateEvent'
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
  | 'AddChild'
  | 'ChangeUserType'
  | 'DeleteUser'
  | 'PurgeDeletedAccounts'
  | 'ResetTwoFactor'
  | 'SetAdmin'
  | 'UpdateUser'

/** Kind of thing an audit entry points at. */
export type AuditEntityType =
  | 'Activity'
  | 'Event'
  | 'EventCategoryType'
  | 'NewsItem'
  | 'Partner'
  | 'Resource'
  | 'StoredFile'
  | 'TermsDocument'
  | 'User'

/** How an audited command ended. */
export type AuditOutcome = 'Succeeded' | 'Rejected' | 'Failed'

/** One thing an audited command named. */
export interface AuditEntityRef {
  readonly type: AuditEntityType
  readonly id: string
}

/** The main thing of an entry; `name` is the full name of a user that still exists, else empty. */
export interface AuditMainEntity extends AuditEntityRef {
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
  readonly entity: AuditMainEntity | null
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
