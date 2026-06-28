import type { AccountRole } from '@/modules/account/domain/entities/account-profile.entity'

export interface AccountChild {
  readonly id: string
  readonly firstName: string
  readonly lastName: string
  readonly birthDate: string
  readonly roles: readonly AccountRole[]
}
