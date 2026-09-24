import type { Gender, TwoFactorMethod } from '@/shared/api/generated/models'

/** Reference to a user status or user type catalog entry; `color` is `null` when none is set. */
export interface UserCatalogRef {
  readonly id: string
  readonly name: string
  readonly color: string | null
}

/**
 * User row and detail model for admin screens, mapped from `UserResponse`. `parentId` and
 * `parentName` identify the guardian of a minor; `dependentCount` counts this user's minors. Only
 * dependents have a `birthDate` and only independent accounts a `nationalId`; the missing one is
 * `''`.
 */
export interface User {
  readonly id: string
  readonly firstName: string
  readonly lastName: string
  readonly email: string
  readonly phone: string
  /** Optional second contact phone; `''` when the user has none. */
  readonly secondaryPhone: string
  readonly birthDate: string
  /** Normalized DNI or NIE of an independent account. */
  readonly nationalId: string
  /** Whether the user agreed to receive promotional content; always `false` for dependents. */
  readonly promotionalConsent: boolean
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
 * dependent keeps its guardian, needs a birth date that keeps it a minor whenever it changes and
 * may omit email, phones, DNI/NIE and consent, while any other account requires email, phone and
 * DNI/NIE, may add a secondary phone different from the phone, and is refused a guardian or a birth
 * date. Dependents are only created under their guardian, never by editing an existing account.
 */
export interface UpdateUserInput {
  readonly firstName: string
  readonly lastName: string
  readonly email: string | null
  readonly phone: string | null
  /** Optional second contact phone; `null` removes it and is always sent for a dependent. */
  readonly secondaryPhone: string | null
  /** `YYYY-MM-DD` for a dependent; `null` for an independent account. */
  readonly birthDate: string | null
  /** Normalized DNI or NIE of an independent account; `null` for a dependent. */
  readonly nationalId: string | null
  /** Agreement to receive promotional content; ignored by the API for a dependent. */
  readonly promotionalConsent: boolean
  readonly gender: Gender
  readonly parentId: string | null
  /**
   * Password of the signed-in user, required by the API whenever the change replaces the email, the
   * phone or the secondary phone of the account. `null` for every other edit.
   */
  readonly currentPassword: string | null
}
