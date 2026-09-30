import type {
  LoginChallengeResponse,
  UserResponse,
  UserStatusTypeResponse,
  UserTypeResponse,
} from '@/shared/api/generated/models'

/** Wire `UserResponse` of an adult participant, as `/api/auth/me` and the users endpoints return it. */
export function buildUserResponse(overrides: Partial<UserResponse> = {}): UserResponse {
  return {
    id: 'user-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    email: 'ada@example.test',
    phone: '600000000',
    secondaryPhone: null,
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
    status: { id: 'status-active', name: 'Active', color: '#00FF00' },
    isAdmin: false,
    isInitialAdmin: false,
    type: { id: 'type-participant', name: 'Participant', color: '#00AA00' },
    twoFactorMethod: 'Email',
    earlySignupEligible: false,
    ...overrides,
  }
}

/** Wire minor of the default adult, as the users endpoints return it. */
export function buildDependentResponse(overrides: Partial<UserResponse> = {}): UserResponse {
  return buildUserResponse({
    id: 'child-1',
    firstName: 'Byron',
    lastName: 'Lovelace',
    email: null,
    phone: null,
    nationalId: null,
    birthDate: '2015-03-02',
    gender: 'Male',
    parentId: 'user-1',
    parentName: 'Ada Lovelace',
    dependentCount: null,
    ...overrides,
  })
}

/** Pending second-factor challenge as returned by `/api/auth/login` and its `GET` twin. */
export function buildLoginChallenge(
  overrides: Partial<LoginChallengeResponse> = {},
): LoginChallengeResponse {
  return { method: 'Email', maskedEmail: 'a***@example.test', ...overrides }
}

/** User types catalog. */
export const userTypes: UserTypeResponse[] = [
  { id: 'type-participant', name: 'Participant', description: '', color: '#00AA00' },
  { id: 'type-member', name: 'Member', description: '', color: '#0000AA' },
]

/** User status catalog. */
export const userStatusTypes: UserStatusTypeResponse[] = [
  { id: 'status-active', name: 'Active', description: '', color: '#00FF00' },
  { id: 'status-blocked', name: 'Blocked', description: '', color: '#FF0000' },
]
