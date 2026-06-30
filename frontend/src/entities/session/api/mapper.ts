import type { UserResponse } from '@/shared/api/generated/models'

import type { AuthRole, AuthUser } from '../model/types'

export function toAuthUser(user: UserResponse): AuthUser {
  const roles: AuthRole[] = (user.roles ?? [])
    .filter((role) => role.id)
    .map((role) => ({ id: role.id as string, name: role.name ?? '' }))

  return {
    id: user.id ?? '',
    firstName: user.firstName ?? '',
    lastName: user.lastName ?? '',
    email: user.email ?? '',
    phone: user.phone ?? '',
    birthDate: user.birthDate ?? '',
    roles,
  }
}
