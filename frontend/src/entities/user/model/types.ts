import type { Gender } from '@/shared/api/generated/models'

export interface UserCatalogRef {
  readonly id: string
  readonly name: string
  readonly color: string | null
}

export interface User {
  readonly id: string
  readonly firstName: string
  readonly lastName: string
  readonly email: string
  readonly phone: string
  readonly birthDate: string
  readonly gender: Gender | null
  readonly isAdmin: boolean
  readonly parentId: string | null
  readonly parentName: string
  readonly dependentCount: number
  readonly status: UserCatalogRef | null
  readonly type: UserCatalogRef | null
}

export interface UpdateUserInput {
  readonly firstName: string
  readonly lastName: string
  readonly email: string | null
  readonly phone: string | null
  readonly birthDate: string
  readonly gender: Gender
  readonly parentId: string | null
}
