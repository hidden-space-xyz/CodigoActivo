import type { AuthUser } from '@/entities/session/model/types'
import type { UserResponse } from '@/shared/api/generated/models'

/** Signed-in session user with sensible defaults. */
export function buildAuthUser(overrides: Partial<AuthUser> = {}): AuthUser {
  return {
    id: 'user-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    email: 'ada@example.test',
    phone: '600000000',
    birthDate: '1990-05-10',
    isAdmin: false,
    userTypeId: 'type-participant',
    earlySignupEligible: false,
    ...overrides,
  }
}

/** Wire `UserResponse` as returned by `/api/auth/me` and the users endpoints. */
export function buildUserResponse(overrides: UserResponse = {}): UserResponse {
  return {
    id: 'user-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    email: 'ada@example.test',
    phone: '600000000',
    birthDate: '1990-05-10',
    gender: 'Female',
    lastLoginAt: null,
    createdAt: '2025-01-01T10:00:00Z',
    updatedAt: null,
    parentId: null,
    parentName: null,
    dependentCount: 0,
    status: { id: 'status-active', name: 'Active' },
    isAdmin: false,
    type: { id: 'type-participant', name: 'Participant' },
    ...overrides,
  }
}
