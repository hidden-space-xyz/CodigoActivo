import { getApiAuthMe } from '@/shared/api/generated/endpoints/auth/auth'
import { getApiRegistrationTypes } from '@/shared/api/generated/endpoints/registration-types/registration-types'
import {
  deleteApiUsersUserId,
  getApiUsers,
  patchApiUsersUserIdPassword,
  postApiUsersUserIdChildren,
  putApiUsersUserId,
} from '@/shared/api/generated/endpoints/users/users'
import { ApiError } from '@/shared/api'

import type {
  AddMinorInput,
  ChangePasswordInput,
  UpdateMinorInput,
  UpdateProfileInput,
} from '../model/account-inputs'
import type { AccountChild, AccountProfile, RegistrationType } from '../model/types'
import {
  toAccountChild,
  toAccountProfile,
  toAddMinorRequest,
  toRegistrationType,
  toUpdateMinorRequest,
  toUpdateProfileRequest,
} from './mapper'

export async function getAccountProfileRequest(): Promise<AccountProfile | null> {
  try {
    const response = await getApiAuthMe()
    return toAccountProfile(response.data)
  } catch (error) {
    if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
      return null
    }
    throw error
  }
}

export async function getAccountChildrenRequest(
  parentId: string,
): Promise<readonly AccountChild[]> {
  const { data } = await getApiUsers({ parentId, sort: 'firstName', pageSize: 100 })
  return (data.items ?? []).map(toAccountChild)
}

export async function getRegistrationTypesRequest(
  minor: boolean,
): Promise<readonly RegistrationType[]> {
  const { data } = await getApiRegistrationTypes({ audience: minor ? 'Minor' : 'Adult' })
  return (data ?? []).map(toRegistrationType)
}

export async function updateAccountProfileRequest(
  userId: string,
  input: UpdateProfileInput,
): Promise<AccountProfile> {
  const response = await putApiUsersUserId(userId, toUpdateProfileRequest(input))
  return toAccountProfile(response.data)
}

export async function deleteAccountRequest(userId: string): Promise<void> {
  await deleteApiUsersUserId(userId)
}

export async function changeAccountPasswordRequest(
  userId: string,
  input: ChangePasswordInput,
): Promise<void> {
  await patchApiUsersUserIdPassword(userId, {
    currentPassword: input.currentPassword,
    newPassword: input.newPassword,
  })
}

export async function addAccountChildRequest(
  parentId: string,
  input: AddMinorInput,
): Promise<AccountChild> {
  const response = await postApiUsersUserIdChildren(parentId, toAddMinorRequest(input))
  return toAccountChild(response.data)
}

export async function updateAccountChildRequest(
  childId: string,
  parentId: string,
  input: UpdateMinorInput,
): Promise<AccountChild> {
  const response = await putApiUsersUserId(childId, toUpdateMinorRequest(input, parentId))
  return toAccountChild(response.data)
}

export async function deleteAccountChildRequest(childId: string): Promise<void> {
  await deleteApiUsersUserId(childId)
}
