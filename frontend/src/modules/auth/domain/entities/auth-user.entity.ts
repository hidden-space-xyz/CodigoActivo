export interface AuthRole {
  readonly id: string
  readonly name: string
}

export interface AuthUser {
  readonly id: string
  readonly firstName: string
  readonly lastName: string
  readonly email: string
  readonly phone: string
  readonly birthDate: string
  readonly roles: readonly AuthRole[]
}
