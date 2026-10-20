import type {
  AccountAuditEntryResponse,
  AuthenticatorSetupResponse,
  EventCertificateResponse,
  EventHistoryActivityResponse,
  EventHistoryResponse,
  MyConsentResponse,
  PersonalDataResponse,
  RegisterMinorRequest,
  RegisterRequest,
  SaveEventRatingRequest,
  UpdateUserRequest,
  UserResponse,
} from '@/shared/api/generated/models'
import type { Translate, TranslationKey } from '@/shared/i18n'

import type {
  EventRatingInput,
  MinorInput,
  RegistrationInput,
  UpdateProfileInput,
} from '../model/account-inputs'
import type {
  AccountCertificate,
  AccountChild,
  AccountConsent,
  AccountHistoryActivity,
  AccountHistoryEntry,
  AccountProfile,
  AuthenticatorSetup,
  PersonalDataFile,
} from '../model/types'

/** Maps the signed-in user to their profile; missing contact details become empty text. */
export function toAccountProfile(user: UserResponse): AccountProfile {
  return {
    id: user.id,
    firstName: user.firstName,
    lastName: user.lastName,
    email: user.email ?? '',
    phone: user.phone ?? '',
    secondaryPhone: user.secondaryPhone ?? '',
    nationalId: user.nationalId ?? '',
    newsConsent: user.newsConsent,
    gender: user.gender,
    statusName: user.status.name,
    isAdmin: user.isAdmin,
  }
}

/** Maps the enrollment data returned when an authenticator setup starts. */
export function toAuthenticatorSetup(setup: AuthenticatorSetupResponse): AuthenticatorSetup {
  return { sharedKey: setup.sharedKey, authenticatorUri: setup.authenticatorUri }
}

/** Maps a minor to the reduced shape shown in the household list. */
export function toAccountChild(user: UserResponse): AccountChild {
  return {
    id: user.id,
    firstName: user.firstName,
    lastName: user.lastName,
    birthDate: user.birthDate ?? '',
    gender: user.gender,
  }
}

/**
 * Builds the user update body for the own profile; adults send `null` as both `birthDate` and
 * `parentId`.
 */
export function toUpdateProfileRequest(input: UpdateProfileInput): UpdateUserRequest {
  return {
    firstName: input.firstName,
    lastName: input.lastName,
    email: input.email,
    phone: input.phone,
    secondaryPhone: input.secondaryPhone,
    birthDate: null,
    nationalId: input.nationalId,
    newsConsent: input.newsConsent,
    gender: input.gender,
    parentId: null,
    currentPassword: input.currentPassword,
  }
}

/**
 * Builds the body for registering a minor, alone or with its guardian. Adding a minor is the
 * adult's declaration of being its parent or legal guardian, which every form that adds one states,
 * so the body always carries it.
 */
export function toMinorRequest(input: MinorInput): RegisterMinorRequest {
  return {
    firstName: input.firstName,
    lastName: input.lastName,
    birthDate: input.birthDate,
    gender: input.gender,
    declaresGuardianship: true,
  }
}

/** Builds the user update body for a minor, keeping it linked to `parentId`. */
export function toUpdateMinorRequest(input: MinorInput, parentId: string): UpdateUserRequest {
  return {
    firstName: input.firstName,
    lastName: input.lastName,
    birthDate: input.birthDate,
    gender: input.gender,
    newsConsent: false,
    parentId,
  }
}

function toAccountHistoryActivity(activity: EventHistoryActivityResponse): AccountHistoryActivity {
  return {
    activityId: activity.activityId,
    title: activity.title,
    location: activity.location,
    modality: activity.modalityName,
    participantId: activity.userId,
    participantName: `${activity.firstName} ${activity.lastName}`.trim(),
    isSelf: activity.isSelf,
    roleName: activity.roleTypeName,
    statusName: activity.statusName,
  }
}

/**
 * Maps an attended event with its activities for the account history, joining participant names.
 * Ratings are anonymous and unlimited, so a past entry stays rateable however many were sent.
 */
export function toAccountHistoryEntry(entry: EventHistoryResponse): AccountHistoryEntry {
  return {
    eventId: entry.eventId,
    title: entry.title,
    subtitle: entry.subtitle,
    startsAt: entry.eventStartsAt,
    endsAt: entry.eventEndsAt,
    thumbnailId: entry.thumbnailId,
    isPast: entry.isPast,
    canRate: entry.canRate,
    activities: entry.activities.map(toAccountHistoryActivity),
  }
}

/** Maps a participation certificate issued to the user or one of their minors. */
export function toAccountCertificate(certificate: EventCertificateResponse): AccountCertificate {
  return {
    code: certificate.code,
    eventId: certificate.eventId,
    participantId: certificate.userId,
    firstName: certificate.firstName,
    lastName: certificate.lastName,
    eventTitle: certificate.eventTitle,
    startsAt: certificate.eventStartsAt,
    endsAt: certificate.eventEndsAt,
  }
}

/** Maps a consent of the household as the API returns it. */
export function toAccountConsent(consent: MyConsentResponse): AccountConsent {
  return {
    eventId: consent.eventId,
    eventTitle: consent.eventTitle,
    eventStartsAt: consent.eventStartsAt,
    termsDocumentId: consent.termsDocumentId,
    documentName: consent.documentName,
    personId: consent.personId,
    personName: consent.personName,
    accepted: consent.accepted,
    withdrawn: consent.withdrawn,
    decidedAt: consent.decidedAt,
    decidedByName: consent.decidedByName,
  }
}

/** Builds the rating body, trimming comments and sending blank ones as `null`. */
export function toSaveEventRatingRequest(input: EventRatingInput): SaveEventRatingRequest {
  return {
    score: input.score,
    mostLiked: input.mostLiked.trim() || null,
    leastLiked: input.leastLiked.trim() || null,
    suggestions: input.suggestions.trim() || null,
  }
}

/** Builds the body that registers an adult together with their minors. */
export function toRegisterRequest(input: RegistrationInput): RegisterRequest {
  return {
    ...input.adult,
    password: input.password,
    declaresAdult: input.declaresAdult,
    minors: input.minors.map(toMinorRequest),
  }
}

const CONSENT_SOURCES: Readonly<Record<string, TranslationKey>> = {
  Registration: 'pages.account.personalData.sources.Registration',
  Profile: 'pages.account.personalData.sources.Profile',
  UnsubscribeLink: 'pages.account.personalData.sources.UnsubscribeLink',
  OneClickUnsubscribe: 'pages.account.personalData.sources.OneClickUnsubscribe',
}

function securityLogActor(entry: AccountAuditEntryResponse, t: Translate): string {
  if (entry.ranBySelf) return t('pages.account.personalData.actors.self')
  return t(`pages.account.personalData.actors.${entry.actorType}`)
}

/**
 * Turns the copy of the personal data into the file the user downloads, with Spanish names and the
 * labels the site shows instead of the codes the API answers with.
 */
export function toPersonalDataFile(data: PersonalDataResponse, t: Translate): PersonalDataFile {
  const { account } = data
  return {
    generadoEl: data.generatedAt,
    cuenta: {
      id: account.id,
      nombre: account.firstName,
      apellidos: account.lastName,
      email: account.email ?? null,
      emailNuevoSinConfirmar: account.pendingEmail ?? null,
      telefono: account.phone ?? null,
      segundoTelefono: account.secondaryPhone ?? null,
      dniNie: account.nationalId ?? null,
      genero: t(`entities.user.gender.${account.gender}`),
      aceptaNovedades: account.newsConsent,
      relacionConLaAsociacion: account.userType,
      estado: account.status,
      administraLaWeb: account.isAdmin,
      verificacionEnDosPasos: t(`pages.account.twoFactor.methods.${account.twoFactorMethod}`),
      altaEl: account.createdAt,
      modificadaEl: account.updatedAt ?? null,
      ultimoAccesoEl: account.lastLoginAt ?? null,
      declaraSerMayorDeEdadEl: account.adultDeclaredAt ?? null,
    },
    cambiosDeLaPreferenciaSobreNovedades: data.newsConsentChanges.map((change) => ({
      acepta: change.granted,
      medio: change.source in CONSENT_SOURCES ? t(CONSENT_SOURCES[change.source]!) : change.source,
      fecha: change.changedAt,
    })),
    menores: data.dependents.map((dependent) => ({
      id: dependent.id,
      nombre: dependent.firstName,
      apellidos: dependent.lastName,
      fechaDeNacimiento: dependent.birthDate ?? null,
      genero: t(`entities.user.gender.${dependent.gender}`),
      relacionConLaAsociacion: dependent.userType,
      altaEl: dependent.createdAt,
      modificadoEl: dependent.updatedAt ?? null,
      declarasSerSuPadreMadreOTutorEl: dependent.guardianshipDeclaredAt ?? null,
      teAvisamosDeQueCumple18El: dependent.comingOfAgeNoticeSentAt ?? null,
    })),
    inscripciones: data.signups.map((signup) => ({
      personaId: signup.personId,
      persona: signup.personName,
      evento: signup.eventTitle,
      actividad: signup.activityTitle,
      inicioDeLaActividad: signup.activityStartsAt,
      rol: signup.role,
      estado: signup.status,
      inscritaEl: signup.signedUpAt,
    })),
    decisionesSobreDocumentos: data.termsDecisions.map((decision) => ({
      personaId: decision.personId,
      persona: decision.personName,
      decidioPor: decision.decidedByName,
      evento: decision.eventTitle,
      documento: decision.documentName,
      versionDelDocumento: decision.documentVersion,
      aceptado: decision.accepted,
      fecha: decision.decidedAt,
      firmaPngBase64: decision.signature ?? null,
      cambios: decision.changes.map((change) => ({
        aceptado: change.accepted,
        versionDelDocumento: change.documentVersion,
        fecha: change.changedAt,
        decidioPor: change.decidedByName,
      })),
    })),
    sesionesAbiertas: data.sessions.map((session) => ({
      abiertaEl: session.createdAt,
      caducaEl: session.expiresAt,
    })),
    registroDeSeguridad: data.securityLog.map((entry) => ({
      fecha: entry.occurredAt,
      operacion: t(`pages.admin.audit.actions.${entry.action}`),
      resultado: t(`pages.admin.audit.outcomes.${entry.outcome}`),
      quien: securityLogActor(entry, t),
      ip: entry.clientIp ?? null,
    })),
  }
}
