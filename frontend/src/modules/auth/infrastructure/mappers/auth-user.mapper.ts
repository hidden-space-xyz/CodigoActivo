import type { AuthRole, AuthUser } from '@/modules/auth/domain/entities/auth-user.entity'
import type { UserResponse } from '@/shared/api/generated/models'

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
