import type {
  UpdateUserRequest,
  UserResponse,
  UserStatusResponse,
  UserStatusTypeResponse,
  UserTypeResponse,
  UserTypeSummaryResponse,
} from '@/shared/api/generated/models'

import type { UpdateUserInput, User, UserCatalogRef } from '../model/types'

/** Maps a user status or user type, as a user carries it or a catalog lists it. */
export function toUserCatalogRef(
  entry: UserStatusResponse | UserStatusTypeResponse | UserTypeResponse | UserTypeSummaryResponse,
): UserCatalogRef {
  return { id: entry.id, name: entry.name, color: entry.color }
}

/** Maps a user for the admin screens; missing contact details become empty text. */
export function toUser(user: UserResponse): User {
  return {
    id: user.id,
    firstName: user.firstName,
    lastName: user.lastName,
    email: user.email ?? '',
    phone: user.phone ?? '',
    secondaryPhone: user.secondaryPhone ?? '',
    birthDate: user.birthDate ?? '',
    nationalId: user.nationalId ?? '',
    promotionalConsent: user.promotionalConsent,
    gender: user.gender,
    isAdmin: user.isAdmin,
    isInitialAdmin: user.isInitialAdmin,
    usesAuthenticator: user.twoFactorMethod === 'Authenticator',
    parentId: user.parentId ?? null,
    parentName: user.parentName ?? '',
    dependentCount: user.dependentCount ?? 0,
    status: toUserCatalogRef(user.status),
    type: user.type ? toUserCatalogRef(user.type) : null,
  }
}

/** Builds the body that replaces a user. */
export function toUpdateUserRequest(input: UpdateUserInput): UpdateUserRequest {
  return {
    firstName: input.firstName,
    lastName: input.lastName,
    email: input.email,
    phone: input.phone,
    secondaryPhone: input.secondaryPhone,
    birthDate: input.birthDate,
    nationalId: input.nationalId,
    promotionalConsent: input.promotionalConsent,
    gender: input.gender,
    parentId: input.parentId,
    currentPassword: input.currentPassword,
  }
}
