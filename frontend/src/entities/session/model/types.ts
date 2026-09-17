import type { TwoFactorMethod } from '@/shared/api/generated/models'

/** Signed-in user held by the session; `birthDate` is an ISO date string, empty when unknown. */
export interface AuthUser {
  readonly id: string
  readonly firstName: string
  readonly lastName: string
  readonly email: string
  readonly phone: string
  readonly birthDate: string
  readonly isAdmin: boolean
  readonly userTypeId: string
  readonly earlySignupEligible: boolean
  /** Second factor the user presents after the password; every account has one. */
  readonly twoFactorMethod: TwoFactorMethod
}

/**
 * Pending second step of a login whose password was accepted. `maskedEmail` is the partially
 * hidden address the code went to, and `null` for authenticator applications.
 */
export interface LoginChallenge {
  readonly method: TwoFactorMethod
  readonly maskedEmail: string | null
}
