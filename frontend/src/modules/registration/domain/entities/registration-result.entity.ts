export interface RegistrationResult {
  readonly adultId: string | null
  readonly verificationCode: string | null
  readonly minorCount: number
}
