import type { Gender } from '@/shared/api/generated/models'

export interface AccountProfile {
  readonly id: string
  readonly firstName: string
  readonly lastName: string
  readonly email: string
  readonly phone: string
  readonly birthDate: string
  readonly gender: Gender | null
  readonly statusName: string
  readonly isAdmin: boolean
}

export interface AccountChild {
  readonly id: string
  readonly firstName: string
  readonly lastName: string
  readonly birthDate: string
  readonly gender: Gender | null
}

export interface AccountHistoryActivity {
  readonly activityId: string
  readonly title: string
  readonly location: string
  readonly modality: string
  readonly participantId: string
  readonly participantName: string
  readonly isSelf: boolean
  readonly roleName: string
  readonly statusName: string
}

export interface AccountEventRating {
  readonly score: number
  readonly mostLiked: string
  readonly leastLiked: string
  readonly suggestions: string
}

export interface AccountCertificate {
  readonly code: string
  readonly eventId: string
  readonly participantId: string
  readonly firstName: string
  readonly lastName: string
  readonly isSelf: boolean
  readonly eventTitle: string
  readonly eventSubtitle: string
  readonly startsAt: string
  readonly endsAt: string
}

export interface AccountHistoryEntry {
  readonly eventId: string
  readonly title: string
  readonly subtitle: string
  readonly startsAt: string
  readonly endsAt: string
  readonly thumbnailId: string
  readonly isPast: boolean
  readonly canRate: boolean
  readonly rating: AccountEventRating | null
  readonly activities: readonly AccountHistoryActivity[]
}
