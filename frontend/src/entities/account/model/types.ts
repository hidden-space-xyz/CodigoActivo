export interface AccountRole {
  readonly id: string
  readonly name: string
}

export interface AccountProfile {
  readonly id: string
  readonly firstName: string
  readonly lastName: string
  readonly email: string
  readonly phone: string
  readonly birthDate: string
  readonly statusName: string
  readonly roles: readonly AccountRole[]
}

export interface AccountChild {
  readonly id: string
  readonly firstName: string
  readonly lastName: string
  readonly birthDate: string
  readonly roles: readonly AccountRole[]
}

export interface RegistrationType {
  readonly id: string
  readonly name: string
  readonly description: string
  readonly color: string
}
