import type {
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
  AccountEventRating,
  AccountHistoryActivity,
  AccountHistoryEntry,
  AccountProfile,
} from '../model/types'

export function toAccountProfile(user: UserResponse): AccountProfile {
  return {
    id: user.id ?? '',
    firstName: user.firstName ?? '',
    lastName: user.lastName ?? '',
    email: user.email ?? '',
    phone: user.phone ?? '',
    birthDate: user.birthDate ?? '',
    statusName: user.status?.name ?? '',
    isAdmin: user.isAdmin ?? false,
  }
}

export function toAccountChild(user: UserResponse): AccountChild {
  return {
    id: user.id ?? '',
    firstName: user.firstName ?? '',
    lastName: user.lastName ?? '',
    birthDate: user.birthDate ?? '',
  }
}

export function toUpdateProfileRequest(input: UpdateProfileInput): UpdateUserRequest {
  return {
    firstName: input.firstName,
    lastName: input.lastName,
    email: input.email,
    phone: input.phone,
    birthDate: input.birthDate,
    parentId: null,
  }
}

export function toAddMinorRequest(input: AddMinorInput): RegisterMinorRequest {
  return {
    firstName: input.firstName,
    lastName: input.lastName,
    birthDate: input.birthDate,
  }
}

export function toUpdateMinorRequest(input: UpdateMinorInput, parentId: string): UpdateUserRequest {
  return {
    firstName: input.firstName,
    lastName: input.lastName,
    birthDate: input.birthDate,
    parentId,
  }
}

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

export function toSaveEventRatingRequest(input: EventRatingInput): SaveEventRatingRequest {
  return {
    score: input.score,
    mostLiked: input.mostLiked.trim() || null,
    leastLiked: input.leastLiked.trim() || null,
    suggestions: input.suggestions.trim() || null,
  }
}
