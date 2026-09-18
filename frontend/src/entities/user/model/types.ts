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
 * Fields an admin edits on a user. The backend applies them by age: minors require `parentId` and
 * lose email and phone; adults require email and phone and must not have `parentId`.
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
   * identifiers of the account: another email or phone, or turning a user that still has contact
   * details into a dependent minor. `null` for every other edit.
   */
  readonly currentPassword: string | null
}
