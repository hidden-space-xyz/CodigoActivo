import type { AuthUser } from './types'

/**
 * Part of the admin area a page or action belongs to: the content editors and administrators
 * manage, the people and the blocked data only administrators see, or the audit log the
 * superadministrator and the data protection officer read.
 */
export type StaffAccess = 'editor' | 'admin' | 'audit'

/** Whether `user` may use the `access` part of the admin area, as the API authorizes it. */
export function hasStaffAccess(user: AuthUser | null, access: StaffAccess): boolean {
  if (!user) return false
  if (access === 'editor') return user.staffRole !== 'None'
  if (access === 'admin') return user.staffRole === 'Administrator'
  return user.isSuperAdmin || user.isPrivacyOfficer
}
