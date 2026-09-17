import type { UserResponse } from '@/shared/api/generated/models'
import { EARLY_SIGNUP_USER_TYPE_IDS } from '@/shared/config'

import type { AuthUser } from '../model/types'

/** Maps the signed-in user; early signup eligibility comes from the configured user type ids. */
export function toAuthUser(user: UserResponse): AuthUser {
  const userTypeId = user.type?.id ?? ''
  return {
    id: user.id ?? '',
    firstName: user.firstName ?? '',
    lastName: user.lastName ?? '',
    email: user.email ?? '',
    phone: user.phone ?? '',
    birthDate: user.birthDate ?? '',
    isAdmin: user.isAdmin ?? false,
    userTypeId,
    earlySignupEligible: EARLY_SIGNUP_USER_TYPE_IDS.includes(userTypeId),
  }
}
