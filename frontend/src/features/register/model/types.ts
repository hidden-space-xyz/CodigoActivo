/** Outcome of a registration, mapped from `RegisterResponse`. */
export interface RegistrationResult {
  /** Created adult's id, needed to resend the verification email; `null` if the API omitted it. */
  readonly adultId: string | null
  readonly minorCount: number
}
