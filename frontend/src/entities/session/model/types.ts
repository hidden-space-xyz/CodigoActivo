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
}
