import type {
  AuthenticatorSetupResponse,
  EventCertificateResponse,
  EventHistoryActivityResponse,
  EventHistoryResponse,
  RegisterMinorRequest,
  RegisterRequest,
  RegisterResponse,
  SaveEventRatingRequest,
  UpdateUserRequest,
  UserResponse,
} from '@/shared/api/generated/models'

import type {
  EventRatingInput,
  MinorInput,
  RegistrationInput,
  UpdateProfileInput,
} from '../model/account-inputs'
import type {
  AccountCertificate,
  AccountChild,
  AccountHistoryActivity,
  AccountHistoryEntry,
  AccountProfile,
  AuthenticatorSetup,
  RegistrationResult,
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
    promotionalConsent: user.promotionalConsent,
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
    promotionalConsent: input.promotionalConsent,
    gender: input.gender,
    parentId: null,
    currentPassword: input.currentPassword,
  }
}

/** Builds the body for registering a minor, alone or with its guardian. */
export function toMinorRequest(input: MinorInput): RegisterMinorRequest {
  return {
    firstName: input.firstName,
    lastName: input.lastName,
    birthDate: input.birthDate,
    gender: input.gender,
  }
}

/** Builds the user update body for a minor, keeping it linked to `parentId`. */
export function toUpdateMinorRequest(input: MinorInput, parentId: string): UpdateUserRequest {
  return {
    firstName: input.firstName,
    lastName: input.lastName,
    birthDate: input.birthDate,
    gender: input.gender,
    promotionalConsent: false,
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
    isSelf: certificate.isSelf,
    eventTitle: certificate.eventTitle,
    eventSubtitle: certificate.eventSubtitle,
    startsAt: certificate.eventStartsAt,
    endsAt: certificate.eventEndsAt,
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
  return { ...input.adult, password: input.password, minors: input.minors.map(toMinorRequest) }
}

/** Reduces the register response to what the success step needs. */
export function toRegistrationResult(response: RegisterResponse): RegistrationResult {
  return { adultId: response.adult.id, minorCount: response.minors.length }
}
