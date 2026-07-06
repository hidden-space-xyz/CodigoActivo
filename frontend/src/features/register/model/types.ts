export interface RegistrationResult {
  readonly adultId: string | null
  readonly requiresVerification: boolean
  readonly minorCount: number
}
