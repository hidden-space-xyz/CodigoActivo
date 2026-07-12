import {
  deleteApiUsersUserId,
  getApiUsers,
  getApiUsersUserId,
  patchApiUsersUserIdAdmin,
  patchApiUsersUserIdChangeType,
  putApiUsersUserId,
} from '@/shared/api/generated/endpoints/users/users'
import type {
  GetApiUsersParams,
  UpdateUserRequest,
  UserResponse,
} from '@/shared/api/generated/models'
import { toPage } from '@/shared/api'

export function getUsersPageRequest(
  params: GetApiUsersParams,
): Promise<{ items: UserResponse[]; total: number }> {
  return getApiUsers(params).then(toPage)
}

export async function getUserRequest(id: string) {
  const { data } = await getApiUsersUserId(id)
  return data
}

export function updateUserRequest(id: string, body: UpdateUserRequest) {
  return putApiUsersUserId(id, body).then((r) => r.data)
}

export function deleteUserRequest(id: string) {
  return deleteApiUsersUserId(id)
}

export function changeUserTypeRequest(id: string, roleId: string) {
  return patchApiUsersUserIdChangeType(id, { roleId }).then((r) => r.data)
}

export function setUserAdminRequest(id: string, isAdmin: boolean) {
  return patchApiUsersUserIdAdmin(id, { isAdmin })
}
