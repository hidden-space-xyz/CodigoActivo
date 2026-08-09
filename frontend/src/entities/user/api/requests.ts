import {
  deleteApiUsersUserId,
  getApiUsers,
  getApiUsersUserId,
  patchApiUsersUserIdAdmin,
  patchApiUsersUserIdChangeType,
  putApiUsersUserId,
} from '@/shared/api/generated/endpoints/users/users'
import type { GetApiUsersParams } from '@/shared/api/generated/models'
import { toPage } from '@/shared/api'

import type { UpdateUserInput, User } from '../model/types'
import { toUpdateUserRequest, toUser } from './mapper'

export async function getUsersPageRequest(
  params: GetApiUsersParams,
): Promise<{ items: User[]; total: number }> {
  const { items, total } = await getApiUsers(params).then(toPage)
  return { items: items.map(toUser), total }
}

export async function getUserRequest(id: string): Promise<User | null> {
  const { data } = await getApiUsersUserId(id)
  return data ? toUser(data) : null
}

export function updateUserRequest(id: string, input: UpdateUserInput): Promise<User | null> {
  return putApiUsersUserId(id, toUpdateUserRequest(input)).then((r) =>
    r.data ? toUser(r.data) : null,
  )
}

export function deleteUserRequest(id: string) {
  return deleteApiUsersUserId(id)
}

export function changeUserTypeRequest(id: string, userTypeId: string): Promise<User | null> {
  return patchApiUsersUserIdChangeType(id, { userTypeId }).then((r) =>
    r.data ? toUser(r.data) : null,
  )
}

export function setUserAdminRequest(id: string, isAdmin: boolean) {
  return patchApiUsersUserIdAdmin(id, { isAdmin })
}
