import {
  deleteApiUsersUserId,
  getApiUsers,
  getApiUsersUserId,
  patchApiUsersUserIdChangeType,
  putApiUsersUserId,
} from '@/shared/api/generated/endpoints/users/users'
import type { UpdateUserRequest } from '@/shared/api/generated/models'

export function getUsersRequest(signal?: AbortSignal) {
  return getApiUsers(signal ? { signal } : undefined).then((r) => r.data ?? [])
}

export function getUserRequest(id: string) {
  return getApiUsersUserId(id).then((r) => r.data)
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
