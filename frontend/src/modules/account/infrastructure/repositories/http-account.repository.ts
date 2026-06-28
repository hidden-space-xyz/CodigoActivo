import type { AccountChild } from '@/modules/account/domain/entities/account-child.entity'
import type { AccountProfile } from '@/modules/account/domain/entities/account-profile.entity'
import type { RegistrationType } from '@/modules/account/domain/entities/registration-type.entity'
import type { AccountRepository } from '@/modules/account/domain/repositories/account-repository'
import type {
  AddMinorInput,
  ChangePasswordInput,
  UpdateMinorInput,
  UpdateProfileInput,
} from '@/modules/account/domain/value-objects/account-inputs'
import {
  toAccountChild,
  toAccountProfile,
  toAddMinorRequest,
  toRegistrationType,
  toUpdateMinorRequest,
  toUpdateProfileRequest,
} from '@/modules/account/infrastructure/mappers/account.mapper'
import { getApiAuthMe } from '@/shared/api/generated/endpoints/auth/auth'
import {
  deleteApiUsersUserId,
  getApiUsersRegistrationTypes,
  getApiUsersUserIdChildren,
  patchApiUsersUserIdPassword,
  patchApiUsersUserIdRole,
  postApiUsersUserIdChildren,
  putApiUsersUserId,
} from '@/shared/api/generated/endpoints/users/users'
import { ApiError } from '@/shared/api/http-client'

export class HttpAccountRepository implements AccountRepository {
  async getProfile(): Promise<AccountProfile | null> {
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

  async getChildren(parentId: string): Promise<readonly AccountChild[]> {
    const response = await getApiUsersUserIdChildren(parentId)
    return (response.data ?? []).map(toAccountChild)
  }

  async getRegistrationTypes(minor: boolean): Promise<readonly RegistrationType[]> {
    const response = await getApiUsersRegistrationTypes({ minor })
    return (response.data ?? []).map(toRegistrationType)
  }

  async updateProfile(userId: string, input: UpdateProfileInput): Promise<AccountProfile> {
    const response = await putApiUsersUserId(userId, toUpdateProfileRequest(input))
    return toAccountProfile(response.data)
  }

  async changeRole(userId: string, roleId: string): Promise<AccountProfile> {
    const response = await patchApiUsersUserIdRole(userId, { roleId })
    return toAccountProfile(response.data)
  }

  async changePassword(userId: string, input: ChangePasswordInput): Promise<void> {
    await patchApiUsersUserIdPassword(userId, {
      currentPassword: input.currentPassword,
      newPassword: input.newPassword,
    })
  }

  async addChild(parentId: string, input: AddMinorInput): Promise<AccountChild> {
    const response = await postApiUsersUserIdChildren(parentId, toAddMinorRequest(input))
    return toAccountChild(response.data)
  }

  async updateChild(
    childId: string,
    parentId: string,
    input: UpdateMinorInput,
  ): Promise<AccountChild> {
    const response = await putApiUsersUserId(childId, toUpdateMinorRequest(input, parentId))
    return toAccountChild(response.data)
  }

  async changeChildRole(childId: string, roleId: string): Promise<AccountChild> {
    const response = await patchApiUsersUserIdRole(childId, { roleId })
    return toAccountChild(response.data)
  }

  async deleteChild(childId: string): Promise<void> {
    await deleteApiUsersUserId(childId)
  }
}
