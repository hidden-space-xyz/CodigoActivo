import type { UserResponse } from '@/shared/api/generated/models'

import type { AuthUser } from '../model/types'

export function toAuthUser(user: UserResponse): AuthUser {
  return {
    id: user.id ?? '',
    firstName: user.firstName ?? '',
    lastName: user.lastName ?? '',
    email: user.email ?? '',
    phone: user.phone ?? '',
    birthDate: user.birthDate ?? '',
    isAdmin: user.isAdmin ?? false,
  }
}
