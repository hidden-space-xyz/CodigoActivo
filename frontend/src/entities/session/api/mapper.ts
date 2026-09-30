import type { LoginChallengeResponse, UserResponse } from '@/shared/api/generated/models'

import type { AuthUser, LoginChallenge } from '../model/types'

/** Maps the signed-in user; the API decides whether the account may use the early signup. */
export function toAuthUser(user: UserResponse): AuthUser {
  return {
    id: user.id,
    firstName: user.firstName,
    lastName: user.lastName,
    email: user.email ?? '',
    phone: user.phone ?? '',
    isAdmin: user.isAdmin,
    earlySignupEligible: user.earlySignupEligible,
    twoFactorMethod: user.twoFactorMethod,
  }
}

/** Maps the pending second-factor challenge; `maskedEmail` is `null` for authenticator apps. */
export function toLoginChallenge(challenge: LoginChallengeResponse): LoginChallenge {
  return {
    method: challenge.method,
    maskedEmail: challenge.maskedEmail ?? null,
  }
}
