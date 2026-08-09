import type {
  UpdateUserRequest,
  UserResponse,
  UserStatusResponse,
  UserTypeSummaryResponse,
} from '@/shared/api/generated/models'

import type { UpdateUserInput, User, UserCatalogRef } from '../model/types'

function toCatalogRef(item?: UserStatusResponse | UserTypeSummaryResponse): UserCatalogRef | null {
  if (!item) return null
  return { id: item.id ?? '', name: item.name ?? '', color: item.color ?? null }
}

export function toUser(user: UserResponse): User {
  return {
    id: user.id ?? '',
    firstName: user.firstName ?? '',
    lastName: user.lastName ?? '',
    email: user.email ?? '',
    phone: user.phone ?? '',
    birthDate: user.birthDate ?? '',
    gender: user.gender ?? null,
    isAdmin: user.isAdmin ?? false,
    parentId: user.parentId ?? null,
    parentName: user.parentName ?? '',
    dependentCount: user.dependentCount ?? 0,
    status: toCatalogRef(user.status),
    type: toCatalogRef(user.type),
  }
}

export function toUpdateUserRequest(input: UpdateUserInput): UpdateUserRequest {
  return {
    firstName: input.firstName,
    lastName: input.lastName,
    email: input.email,
    phone: input.phone,
    birthDate: input.birthDate,
    gender: input.gender,
    parentId: input.parentId,
  }
}
