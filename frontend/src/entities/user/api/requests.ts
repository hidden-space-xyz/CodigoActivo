import {
  deleteApiUsersUserId,
  getApiUsers,
  getApiUsersUserId,
  patchApiUsersUserIdAdmin,
  patchApiUsersUserIdChangeType,
  putApiUsersUserId,
} from '@/shared/api/generated/endpoints/users/users'
import type { UpdateUserRequest, UserResponse } from '@/shared/api/generated/models'
import { fetchAllPages, toPage } from '@/shared/api'

export function getUsersRequest(): Promise<UserResponse[]> {
  return fetchAllPages<UserResponse>((page, pageSize) =>
    getApiUsers({ sort: 'firstName', page, pageSize }).then(toPage),
  )
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
