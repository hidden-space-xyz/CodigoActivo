import type { Gender, TwoFactorMethod } from '@/shared/api/generated/models'

/** Reference to a user status or user type catalog entry; `color` is `null` when none is set. */
export interface UserCatalogRef {
  readonly id: string
  readonly name: string
  readonly color: string | null
}

/**
 * User row and detail model for admin screens, mapped from `UserResponse`. `parentId` and
 * `parentName` identify the guardian of a minor; `dependentCount` counts this user's minors.
 */
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
  /** Second factor the user presents at login; administrators can reset it to email. */
  readonly twoFactorMethod: TwoFactorMethod
}

/**
 * Fields an admin edits on a user. The stored account decides which rules the backend applies: a
 * dependent keeps its guardian and may omit email and phone, while any other account requires both
 * and is refused a guardian or a minor birth date. Dependents are only created under their
 * guardian, never by editing an existing account.
 */
export interface UpdateUserInput {
  readonly firstName: string
  readonly lastName: string
  readonly email: string | null
  readonly phone: string | null
  readonly birthDate: string
  readonly gender: Gender
  readonly parentId: string | null
  /**
   * Password of the signed-in user, required by the API whenever the change replaces the login
   * identifiers of the account: another email or phone. `null` for every other edit.
   */
  readonly currentPassword: string | null
}
