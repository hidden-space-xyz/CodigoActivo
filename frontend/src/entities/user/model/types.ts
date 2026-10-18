import type { Gender } from './gender'

/** A user status or user type as a user carries it and the catalogs list it. */
export interface UserCatalogRef {
  readonly id: string
  readonly name: string
  readonly color: string
}

/**
 * User row and detail model for admin screens. `parentId` and `parentName` identify the guardian
 * of a minor; `dependentCount` counts this user's minors. Only dependents have a `birthDate` and
 * only independent accounts a `nationalId`; the missing one is `''`.
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
  /** Whether the user agreed to receive news by email; always `false` for dependents. */
  readonly newsConsent: boolean
  readonly gender: Gender
  readonly isAdmin: boolean
  /** The generic administrator created on first start: it can be neither deleted nor demoted. */
  readonly isInitialAdmin: boolean
  /** Sign-ins are confirmed with an authenticator app instead of emailed codes. */
  readonly usesAuthenticator: boolean
  readonly parentId: string | null
  readonly parentName: string
  readonly dependentCount: number
  readonly status: UserCatalogRef
  readonly type: UserCatalogRef | null
}

/** Filters, sort and page of the admin user list. */
export interface UserListParams {
  readonly id?: string
  readonly name?: string
  readonly email?: string
  readonly phone?: string
  readonly nationalId?: string
  readonly userTypeId?: string
  readonly userStatusTypeId?: string
  readonly isAdmin?: boolean
  readonly newsConsent?: boolean
  readonly parentId?: string
  /** Independent accounts or dependents only; omitted for both. */
  readonly scope?: 'Independent' | 'Dependent'
  readonly birthDateFrom?: string
  readonly birthDateTo?: string
  readonly page?: number
  readonly pageSize?: number
  readonly sort?: string
}
