export interface AccountProfile {
  readonly id: string
  readonly firstName: string
  readonly lastName: string
  readonly email: string
  readonly phone: string
  readonly birthDate: string
  readonly statusName: string
  readonly isAdmin: boolean
}

export interface AccountChild {
  readonly id: string
  readonly firstName: string
  readonly lastName: string
  readonly birthDate: string
}
