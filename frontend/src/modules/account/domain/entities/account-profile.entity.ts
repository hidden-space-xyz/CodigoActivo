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
