import type { AuthUser } from './types'

/**
 * Part of the admin area a page or action belongs to: the content editors and administrators
 * manage, the people only administrators see, the audit log the superadministrator and the data
 * protection officer read, or the blocked data administrators and that officer reach.
 */
export type StaffAccess = 'editor' | 'admin' | 'audit' | 'blocked-data'

/** Whether `user` may use the `access` part of the admin area, as the API authorizes it. */
export function hasStaffAccess(user: AuthUser | null, access: StaffAccess): boolean {
  if (!user) return false
  if (access === 'editor') return user.staffRole !== 'None'
  if (access === 'admin') return user.staffRole === 'Administrator'
  if (access === 'audit') return user.isSuperAdmin || user.isPrivacyOfficer
  return user.staffRole === 'Administrator' || user.isPrivacyOfficer
}
