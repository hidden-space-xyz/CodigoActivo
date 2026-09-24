import type { AuthUser } from '@/entities/session/model/types'
import type { LoginChallengeResponse, UserResponse } from '@/shared/api/generated/models'

/** Signed-in session user with sensible defaults. */
export function buildAuthUser(overrides: Partial<AuthUser> = {}): AuthUser {
  return {
    id: 'user-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    email: 'ada@example.test',
    phone: '600000000',
    isAdmin: false,
    userTypeId: 'type-participant',
    earlySignupEligible: false,
    twoFactorMethod: 'Email',
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
    birthDate: null,
    nationalId: '12345678Z',
    promotionalConsent: false,
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
    twoFactorMethod: 'Email',
    ...overrides,
  }
}

/** Pending second-factor challenge as returned by `/api/auth/login` and its `GET` twin. */
export function buildLoginChallenge(
  overrides: LoginChallengeResponse = {},
): LoginChallengeResponse {
  return { method: 'Email', maskedEmail: 'a***@example.test', ...overrides }
}
