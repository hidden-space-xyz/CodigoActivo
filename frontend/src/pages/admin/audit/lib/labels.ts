import type { TranslationKey, Translate } from '@/shared/i18n'

import type {
  AuditAction,
  AuditActorType,
  AuditEntityRef,
  AuditEntityType,
  AuditEntry,
  AuditOutcome,
} from '../model/types'

/** Element Plus tag colour an outcome is shown with. */
type OutcomeTagType = 'success' | 'warning' | 'danger'

/** Tells whether a message exists, like `te` of `useI18n`. */
export type HasMessage = (key: string) => boolean

/** Every action the audit log names, in the order the API declares them. */
export const AUDIT_ACTIONS: readonly AuditAction[] = [
  'BeginAuthenticatorSetup',
  'ChangePassword',
  'CompleteLogin',
  'ConfirmAuthenticator',
  'ConfirmEmailChange',
  'DeleteOwnAccount',
  'DisableAuthenticator',
  'EndSession',
  'ForgotPassword',
  'Login',
  'Register',
  'PurgeUnverifiedAccounts',
  'RequestConfirmationCode',
  'ResetPassword',
  'VerifyUser',
  'AssignActivity',
  'AssignHousehold',
  'ChangeAssignmentRole',
  'ChangeAssignmentStatus',
  'CreateActivity',
  'DeleteActivity',
  'UnassignActivity',
  'UpdateActivity',
  'SendEmailToEventAttendees',
  'SendEmailToUser',
  'SendEmailToUsers',
  'Unsubscribe',
  'CreateEventCategoryType',
  'DeleteEventCategoryType',
  'UpdateEventCategoryType',
  'CreateInventoryCategory',
  'UpdateInventoryCategory',
  'DeleteInventoryCategory',
  'AddInventoryEntry',
  'UpdateInventoryEntry',
  'DeleteInventoryEntry',
  'CreateEvent',
  'DeleteEvent',
  'UpdateEvent',
  'WithdrawTermsConsent',
  'CreateFile',
  'DeleteFile',
  'CreateNewsItem',
  'DeleteNewsItem',
  'UpdateNewsItem',
  'CreatePartner',
  'DeletePartner',
  'UpdatePartner',
  'CreateResource',
  'DeleteResource',
  'UpdateResource',
  'CreateTermsDocument',
  'DeleteTermsDocument',
  'UpdateTermsDocument',
  'PublishLegalPolicy',
  'AddChild',
  'ChangeUserType',
  'DeleteUser',
  'PurgeAdultDependents',
  'ResetTwoFactor',
  'SendComingOfAgeNotices',
  'SetStaffRole',
  'DesignatePrivacyOfficer',
  'UpdateUser',
  'PurgeDataHistory',
  'RequestBlockedDataConsultationCode',
  'OpenBlockedDataConsultation',
  'SearchBlockedData',
  'ExportBlockedData',
  'PlaceBlockedDataHold',
  'LiftBlockedDataHold',
  'SendConsentWithdrawalNotices',
  'SendBlockedDataConsultationNotices',
  'PasswordLocked',
  'TwoFactorLocked',
  'AccessDenied',
  'GetPersonalData',
  'GetEventTermsSignature',
  'GetEventRoster',
  'GetEventBadges',
]

/** Every kind of actor, in the order the actor filter lists them. */
export const AUDIT_ACTOR_TYPES: readonly AuditActorType[] = ['User', 'Anonymous', 'System']

/** Every kind of entity, in the order the entity filter lists them. */
export const AUDIT_ENTITY_TYPES: readonly AuditEntityType[] = [
  'User',
  'Event',
  'Activity',
  'EventCategoryType',
  'TermsDocument',
  'NewsItem',
  'Resource',
  'Partner',
  'StoredFile',
  'InventoryCategory',
  'InventoryColumn',
  'InventoryColumnOption',
  'InventoryEntry',
  'LegalPolicy',
  'BlockedDataConsultation',
  'BlockedDataHold',
]

/**
 * Kinds of entity the audit log never names: a consultation of the blocked data and a hold on it,
 * which «Archivo legal» lists by their request.
 */
type UnnamedEntityType = 'BlockedDataConsultation' | 'BlockedDataHold'

/** Every outcome, in the order the outcome filter lists them. */
export const AUDIT_OUTCOMES: readonly AuditOutcome[] = ['Succeeded', 'Rejected', 'Failed']

const OUTCOME_TAG_TYPES: Record<AuditOutcome, OutcomeTagType> = {
  Succeeded: 'success',
  Rejected: 'warning',
  Failed: 'danger',
}

/** Translation key of the name of a command. */
export function actionLabelKey(action: AuditAction): TranslationKey {
  return `pages.admin.audit.actions.${action}`
}

/** Translation key of the name of a kind of actor. */
export function actorTypeLabelKey(actorType: AuditActorType): TranslationKey {
  return `pages.admin.audit.actorTypes.${actorType}`
}

/** Translation key of the name of a kind of entity. */
export function entityTypeLabelKey(entityType: AuditEntityType): TranslationKey {
  return `pages.admin.audit.entityTypes.${entityType}`
}

/** Translation key of the label of a kind of entity that no longer exists. */
function deletedEntityLabelKey(
  entityType: Exclude<AuditEntityType, UnnamedEntityType>,
): TranslationKey {
  return `pages.admin.audit.deletedEntities.${entityType}`
}

/** An entity without a name: by its kind when the audit log never names it, or as removed. */
function unnamedEntityLabel(entity: AuditEntityRef, t: Translate): string {
  return entity.type === 'BlockedDataConsultation' || entity.type === 'BlockedDataHold'
    ? t(entityTypeLabelKey(entity.type))
    : t(deletedEntityLabelKey(entity.type))
}

/** Translation key of the name of an outcome. */
export function outcomeLabelKey(outcome: AuditOutcome): TranslationKey {
  return `pages.admin.audit.outcomes.${outcome}`
}

/** Tag colour of an outcome: green when it succeeded, amber when refused, red when it failed. */
export function outcomeTagType(outcome: AuditOutcome): OutcomeTagType {
  return OUTCOME_TAG_TYPES[outcome]
}

/**
 * Who ran a command: the user's name, «Cuenta eliminada» for an account erased since, or the name
 * of the kind of actor for an anonymous caller or the system.
 */
export function actorLabel(entry: AuditEntry, t: Translate): string {
  if (entry.actorType !== 'User') return t(actorTypeLabelKey(entry.actorType))
  return entry.actorName || t(deletedEntityLabelKey('User'))
}

/**
 * An entity by its name, by its kind when the audit log never names it, or as removed, such as
 * «Evento eliminado», once it no longer exists.
 */
export function entityLabel(entity: AuditEntityRef, t: Translate): string {
  return entity.name || unnamedEntityLabel(entity, t)
}

/**
 * A related entity with its kind and name, such as «Usuario: Marta Miembro», by its kind alone
 * when the audit log never names it, or as removed.
 */
export function relatedLabel(entity: AuditEntityRef, t: Translate): string {
  return entity.name
    ? `${t(entityTypeLabelKey(entity.type))}: ${entity.name}`
    : unnamedEntityLabel(entity, t)
}

/**
 * Message of the code that refused a command: the same message the user saw for a code the API
 * sends, a label of its own for a code that never leaves the server, or the code itself.
 */
export function errorCodeLabel(code: string, t: Translate, has: HasMessage): string {
  for (const key of [`errors.${code}`, `pages.admin.audit.codes.${code}`]) {
    if (has(key)) return t(key as TranslationKey)
  }
  return code
}
