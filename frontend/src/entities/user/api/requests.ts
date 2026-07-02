import { fetchODataList } from '@/shared/api'
import {
  deleteApiUsersUserId,
  getApiUsersUserId,
  patchApiUsersUserIdChangeType,
  putApiUsersUserId,
} from '@/shared/api/generated/endpoints/users/users'
import type { UpdateUserRequest, UserResponse } from '@/shared/api/generated/models'

export function getUsersRequest() {
  return fetchODataList<UserResponse>('Users', { orderBy: 'firstName asc', top: 1000 }).then(
    (page) => page.items,
  )
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
