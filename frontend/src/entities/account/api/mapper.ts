import type { RegistrationTypeResponse } from '@/shared/api/generated/models'
import type {
  RegisterMinorRequest,
  UpdateUserRequest,
  UserResponse,
} from '@/shared/api/generated/models'

import type { AddMinorInput, UpdateMinorInput, UpdateProfileInput } from '../model/account-inputs'
import type { AccountChild, AccountProfile, AccountType, RegistrationType } from '../model/types'

function toType(user: UserResponse): AccountType | null {
  return user.type?.id ? { id: user.type.id, name: user.type.name ?? '' } : null
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
    isAdmin: user.isAdmin ?? false,
    type: toType(user),
  }
}

export function toAccountChild(user: UserResponse): AccountChild {
  return {
    id: user.id ?? '',
    firstName: user.firstName ?? '',
    lastName: user.lastName ?? '',
    birthDate: user.birthDate ?? '',
    type: toType(user),
  }
}

export function toRegistrationType(type: RegistrationTypeResponse): RegistrationType {
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
