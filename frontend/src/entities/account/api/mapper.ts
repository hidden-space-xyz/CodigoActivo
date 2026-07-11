import type {
  RegisterMinorRequest,
  UpdateUserRequest,
  UserResponse,
} from '@/shared/api/generated/models'

import type { AddMinorInput, UpdateMinorInput, UpdateProfileInput } from '../model/account-inputs'
import type { AccountChild, AccountProfile } from '../model/types'

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
  }
}

export function toAccountChild(user: UserResponse): AccountChild {
  return {
    id: user.id ?? '',
    firstName: user.firstName ?? '',
    lastName: user.lastName ?? '',
    birthDate: user.birthDate ?? '',
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
