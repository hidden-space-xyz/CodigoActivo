export interface RegistrationResult {
  readonly adultId: string | null
  readonly verificationCode: string | null
  readonly minorCount: number
}

export interface RegistrationType {
  readonly id: string
  readonly name: string
  readonly description: string
  readonly color: string
}
