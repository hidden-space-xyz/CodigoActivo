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

const SHORT_ID_LENGTH = 8

/** Every command the audit log names, in the order the API declares them. */
export const AUDIT_ACTIONS: readonly AuditAction[] = [
  'BeginAuthenticatorSetup',
  'ChangePassword',
  'CompleteLogin',
  'ConfirmAuthenticator',
  'ConfirmEmailChange',
  'DeleteOwnAccount',
  'DisableAuthenticator',
  'EndLoginChallenge',
  'EndSession',
  'ForgotPassword',
  'Login',
  'Register',
  'RemoveExpiredSessions',
  'PurgeUnverifiedAccounts',
  'PurgeUnusedFiles',
  'RequestAccountDeletionCode',
  'ResendTwoFactorCode',
  'ResendVerification',
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
  'CreateEvent',
  'DeleteEvent',
  'SetEventFeatured',
  'UpdateEvent',
  'CreateFile',
  'DeleteFile',
  'CreateNewsItem',
  'DeleteNewsItem',
  'SetNewsItemFeatured',
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
  'AddChild',
  'ChangeUserType',
  'DeleteUser',
  'PurgeAdultDependents',
  'PurgeDeletedAccounts',
  'ResetTwoFactor',
  'SendComingOfAgeNotices',
  'SetAdmin',
  'UpdateUser',
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
]

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
function deletedEntityLabelKey(entityType: AuditEntityType): TranslationKey {
  return `pages.admin.audit.deletedEntities.${entityType}`
}

/** Translation key of the name of an outcome. */
export function outcomeLabelKey(outcome: AuditOutcome): TranslationKey {
  return `pages.admin.audit.outcomes.${outcome}`
}

/** Tag colour of an outcome: green when it succeeded, amber when refused, red when it failed. */
export function outcomeTagType(outcome: AuditOutcome): OutcomeTagType {
  return OUTCOME_TAG_TYPES[outcome]
}

/** First characters of an identifier, enough to tell entries apart at a glance. */
export function shortId(id: string): string {
  return id.slice(0, SHORT_ID_LENGTH)
}

/**
 * Who ran a command: the user's name, «Cuenta eliminada» for an account erased since, or the name
 * of an anonymous caller or of the system.
 */
export function actorLabel(entry: AuditEntry, t: Translate): string {
  if (entry.actorType !== 'User') return t(actorTypeLabelKey(entry.actorType))
  return entry.actorName || t(deletedEntityLabelKey('User'))
}

/** An entity by its name, or as removed, such as «Evento eliminado», once it no longer exists. */
export function entityLabel(entity: AuditEntityRef, t: Translate): string {
  return entity.name || t(deletedEntityLabelKey(entity.type))
}

/** A related entity by its kind and name, such as «Usuario: Marta Miembro», or as removed. */
export function relatedLabel(entity: AuditEntityRef, t: Translate): string {
  return entity.name
    ? `${t(entityTypeLabelKey(entity.type))}: ${entity.name}`
    : t(deletedEntityLabelKey(entity.type))
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
