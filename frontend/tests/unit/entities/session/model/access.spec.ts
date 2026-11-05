import { describe, expect, it } from 'vitest'

import { hasStaffAccess, type AuthUser, type StaffAccess } from '@/entities/session'

import { buildAuthUser } from '../../../../support/models'

const ACCESSES: readonly StaffAccess[] = ['editor', 'admin', 'audit', 'blocked-data']

function accessesOf(user: AuthUser | null): StaffAccess[] {
  return ACCESSES.filter((access) => hasStaffAccess(user, access))
}

describe('hasStaffAccess', () => {
  it('opens nothing to guests or to accounts without a cargo or designation', () => {
    expect(accessesOf(null)).toEqual([])
    expect(accessesOf(buildAuthUser())).toEqual([])
  })

  it('opens the content to editors and all but the audit to administrators', () => {
    expect(accessesOf(buildAuthUser({ staffRole: 'Editor' }))).toEqual(['editor'])
    expect(accessesOf(buildAuthUser({ staffRole: 'Administrator' }))).toEqual([
      'editor',
      'admin',
      'blocked-data',
    ])
  })

  it('opens everything to the superadministrator', () => {
    expect(accessesOf(buildAuthUser({ staffRole: 'Administrator', isSuperAdmin: true }))).toEqual(
      ACCESSES,
    )
  })

  it('opens the audit and the blocked data to the data protection officer whatever their cargo', () => {
    expect(accessesOf(buildAuthUser({ isPrivacyOfficer: true }))).toEqual(['audit', 'blocked-data'])
    expect(accessesOf(buildAuthUser({ staffRole: 'Editor', isPrivacyOfficer: true }))).toEqual([
      'editor',
      'audit',
      'blocked-data',
    ])
  })
})
