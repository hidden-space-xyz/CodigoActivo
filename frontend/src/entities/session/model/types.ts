/** Second factor a user presents after the password: an emailed code or an authenticator app. */
type TwoFactorMethod = 'Email' | 'Authenticator'

/** Cargo of an account in the admin area; an administrator can do everything an editor can. */
type StaffRole = 'None' | 'Editor' | 'Administrator'

/** Signed-in user held by the session; missing text fields are empty strings. */
export interface AuthUser {
  readonly id: string
  readonly firstName: string
  readonly lastName: string
  readonly email: string
  readonly phone: string
  /** Cargo of the account; `None` for those who only use the public site. */
  readonly staffRole: StaffRole
  /**
   * The superadministrator, the initial account: the only one that changes cargos, designates the
   * data protection officer and deletes other accounts.
   */
  readonly isSuperAdmin: boolean
  /** The account designated as the data protection officer, whatever its cargo. */
  readonly isPrivacyOfficer: boolean
  /** Whether the account may sign up during the early signup, as the API decides. */
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
