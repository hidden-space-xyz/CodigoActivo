import type {
  EventCertificateResponse,
  EventHistoryActivityResponse,
  EventHistoryResponse,
  EventRatingResponse,
  RegisterMinorRequest,
  SaveEventRatingRequest,
  UpdateUserRequest,
  UserResponse,
} from '@/shared/api/generated/models'

import type {
  AddMinorInput,
  EventRatingInput,
  UpdateMinorInput,
  UpdateProfileInput,
} from '../model/account-inputs'
import type {
  AccountChild,
  AccountCertificate,
  AccountEventRating,
  AccountHistoryActivity,
  AccountHistoryEntry,
  AccountProfile,
} from '../model/types'

/** Maps the signed-in user's `UserResponse` to a profile, defaulting missing text to `''`. */
export function toAccountProfile(user: UserResponse): AccountProfile {
  return {
    id: user.id ?? '',
    firstName: user.firstName ?? '',
    lastName: user.lastName ?? '',
    email: user.email ?? '',
    phone: user.phone ?? '',
    birthDate: user.birthDate ?? '',
    gender: user.gender ?? null,
    statusName: user.status?.name ?? '',
    isAdmin: user.isAdmin ?? false,
  }
}

/** Maps a minor's `UserResponse` to the reduced shape shown in the household list. */
export function toAccountChild(user: UserResponse): AccountChild {
  return {
    id: user.id ?? '',
    firstName: user.firstName ?? '',
    lastName: user.lastName ?? '',
    birthDate: user.birthDate ?? '',
    gender: user.gender ?? null,
  }
}

/** Builds the user update body for the own profile; `parentId` is `null` for adults. */
export function toUpdateProfileRequest(input: UpdateProfileInput): UpdateUserRequest {
  return {
    firstName: input.firstName,
    lastName: input.lastName,
    email: input.email,
    phone: input.phone,
    birthDate: input.birthDate,
    gender: input.gender,
    parentId: null,
  }
}

/** Builds the body for registering a minor under the current account. */
export function toAddMinorRequest(input: AddMinorInput): RegisterMinorRequest {
  return {
    firstName: input.firstName,
    lastName: input.lastName,
    birthDate: input.birthDate,
    gender: input.gender,
  }
}

/** Builds the user update body for a minor, keeping it linked to `parentId`. */
export function toUpdateMinorRequest(input: UpdateMinorInput, parentId: string): UpdateUserRequest {
  return {
    firstName: input.firstName,
    lastName: input.lastName,
    birthDate: input.birthDate,
    gender: input.gender,
    parentId,
  }
}

/** Maps a stored event rating; missing score becomes `0` and missing comments `''`. */
export function toAccountEventRating(rating: EventRatingResponse): AccountEventRating {
  return {
    score: rating.score ?? 0,
    mostLiked: rating.mostLiked ?? '',
    leastLiked: rating.leastLiked ?? '',
    suggestions: rating.suggestions ?? '',
  }
}

function toAccountHistoryActivity(activity: EventHistoryActivityResponse): AccountHistoryActivity {
  return {
    activityId: activity.activityId ?? '',
    title: activity.title ?? '',
    location: activity.location ?? '',
    modality: activity.modalityName ?? '',
    participantId: activity.userId ?? '',
    participantName: `${activity.firstName ?? ''} ${activity.lastName ?? ''}`.trim(),
    isSelf: activity.isSelf ?? false,
    roleName: activity.roleTypeName ?? '',
    statusName: activity.statusName ?? '',
  }
}

/**
 * Maps an attended event with its activities for the account history, joining participant names
 * and leaving `rating` as `null` when the user has not rated the event.
 */
export function toAccountHistoryEntry(entry: EventHistoryResponse): AccountHistoryEntry {
  return {
    eventId: entry.eventId ?? '',
    title: entry.title ?? '',
    subtitle: entry.subtitle ?? '',
    startsAt: entry.eventStartsAt ?? '',
    endsAt: entry.eventEndsAt ?? '',
    thumbnailId: entry.thumbnailId ?? '',
    isPast: entry.isPast ?? false,
    canRate: entry.canRate ?? false,
    rating: entry.myRating ? toAccountEventRating(entry.myRating) : null,
    activities: (entry.activities ?? []).map(toAccountHistoryActivity),
  }
}

/** Maps a participation certificate issued to the user or one of their minors. */
export function toAccountCertificate(certificate: EventCertificateResponse): AccountCertificate {
  return {
    code: certificate.code ?? '',
    eventId: certificate.eventId ?? '',
    participantId: certificate.userId ?? '',
    firstName: certificate.firstName ?? '',
    lastName: certificate.lastName ?? '',
    isSelf: certificate.isSelf ?? false,
    eventTitle: certificate.eventTitle ?? '',
    eventSubtitle: certificate.eventSubtitle ?? '',
    startsAt: certificate.eventStartsAt ?? '',
    endsAt: certificate.eventEndsAt ?? '',
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
