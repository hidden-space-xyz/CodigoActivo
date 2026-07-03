import {
  deleteApiUsersUserId,
  getApiUsers,
  getApiUsersUserId,
  patchApiUsersUserIdChangeType,
  putApiUsersUserId,
} from '@/shared/api/generated/endpoints/users/users'
import type { UpdateUserRequest } from '@/shared/api/generated/models'

export async function getUsersRequest() {
  const { data } = await getApiUsers({ sort: 'firstName', pageSize: 100 })
  return data.items ?? []
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
