import type { Gender, TwoFactorMethod } from '@/shared/api/generated/models'

/** Signed-in user's own data shown and edited on the account page, mapped from `UserResponse`. */
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
  /** Second factor asked for at login: an emailed code or an authenticator application. */
  readonly twoFactorMethod: TwoFactorMethod
}

/**
 * Enrollment data of an authenticator application that still has to be confirmed with its first
 * code. `sharedKey` is grouped for manual entry; `authenticatorUri` is what the QR code encodes.
 */
export interface AuthenticatorSetup {
  readonly sharedKey: string
  readonly authenticatorUri: string
}

/** Minor managed by the signed-in adult, mapped from `UserResponse`. */
export interface AccountChild {
  readonly id: string
  readonly firstName: string
  readonly lastName: string
  readonly birthDate: string
  readonly gender: Gender | null
}

/** One household member's participation in an activity of a past or upcoming event. */
export interface AccountHistoryActivity {
  readonly activityId: string
  readonly title: string
  readonly location: string
  readonly modality: string
  readonly participantId: string
  readonly participantName: string
  /** `true` when the participant is the signed-in user rather than one of their minors. */
  readonly isSelf: boolean
  readonly roleName: string
  readonly statusName: string
}

/** Participation certificate for the user or a minor, rendered client-side as a printable sheet. */
export interface AccountCertificate {
  /** Reference code printed on the certificate. */
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

/** Event in the account history, grouping the activities the household joined. */
export interface AccountHistoryEntry {
  readonly eventId: string
  readonly title: string
  readonly subtitle: string
  readonly startsAt: string
  readonly endsAt: string
  readonly thumbnailId: string
  readonly isPast: boolean
  readonly canRate: boolean
  /** `true` once the user submitted a rating; ratings are anonymous and cannot be edited or reread. */
  readonly hasRated: boolean
  readonly activities: readonly AccountHistoryActivity[]
}
