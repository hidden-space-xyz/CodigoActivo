import type { AccountChild } from '@/modules/account/domain/entities/account-child.entity'
import type { AccountRole, AccountProfile } from '@/modules/account/domain/entities/account-profile.entity'
import type { RegistrationType } from '@/modules/account/domain/entities/registration-type.entity'
import type {
  AddMinorInput,
  UpdateMinorInput,
  UpdateProfileInput,
} from '@/modules/account/domain/value-objects/account-inputs'
import type {
  RegisterMinorRequest,
  UpdateUserRequest,
  UserResponse,
  UserTypeResponse,
} from '@/shared/api/generated/models'

function toRoles(user: UserResponse): AccountRole[] {
  return (user.roles ?? [])
    .filter((role) => role.id)
    .map((role) => ({ id: role.id as string, name: role.name ?? '' }))
}

export function toAccountProfile(user: UserResponse): AccountProfile {
  return {
    id: user.id ?? '',
    firstName: user.firstName ?? '',
    lastName: user.lastName ?? '',
    email: user.email ?? '',
    phone: user.phone ?? '',
    birthDate: user.birthDate ?? '',
    statusName: user.status?.name ?? '',
    roles: toRoles(user),
  }
}

export function toAccountChild(user: UserResponse): AccountChild {
  return {
    id: user.id ?? '',
    firstName: user.firstName ?? '',
    lastName: user.lastName ?? '',
    birthDate: user.birthDate ?? '',
    roles: toRoles(user),
  }
}

export function toRegistrationType(type: UserTypeResponse): RegistrationType {
  return {
    id: type.id ?? '',
    name: type.name ?? '',
    description: type.description ?? '',
    color: type.color ?? '',
  }
}

export function toUpdateProfileRequest(input: UpdateProfileInput): UpdateUserRequest {
  return {
    firstName: input.firstName,
    lastName: input.lastName,
    email: input.email,
    phone: input.phone,
    birthDate: input.birthDate,
    parentId: null,
  }
}

export function toAddMinorRequest(input: AddMinorInput): RegisterMinorRequest {
  return {
    firstName: input.firstName,
    lastName: input.lastName,
    birthDate: input.birthDate,
    roleId: input.roleId,
  }
}

export function toUpdateMinorRequest(input: UpdateMinorInput, parentId: string): UpdateUserRequest {
  return {
    firstName: input.firstName,
    lastName: input.lastName,
    birthDate: input.birthDate,
    parentId,
  }
}
