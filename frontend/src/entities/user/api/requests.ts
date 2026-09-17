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

/** Fetches one page of the admin users table, mapped to `User`. */
export async function getUsersPageRequest(
  params: GetApiUsersParams,
): Promise<{ items: User[]; total: number }> {
  const { items, total } = await getApiUsers(params).then(toPage)
  return { items: items.map(toUser), total }
}

/** Loads a user by id; unlike other detail requests a 404 throws instead of resolving `null`. */
export async function getUserRequest(id: string): Promise<User | null> {
  const { data } = await getApiUsersUserId(id)
  return data ? toUser(data) : null
}

/** Replaces the user profile and resolves to the updated user. */
export function updateUserRequest(id: string, input: UpdateUserInput): Promise<User | null> {
  return putApiUsersUserId(id, toUpdateUserRequest(input)).then((r) =>
    r.data ? toUser(r.data) : null,
  )
}

/** Deletes a user (admin only); resolves with the raw 204 response. */
export function deleteUserRequest(id: string) {
  return deleteApiUsersUserId(id)
}

/** Assigns another user type and resolves to the updated user. */
export function changeUserTypeRequest(id: string, userTypeId: string): Promise<User | null> {
  return patchApiUsersUserIdChangeType(id, { userTypeId }).then((r) =>
    r.data ? toUser(r.data) : null,
  )
}

/**
 * Grants or revokes the admin role; resolves with the raw response. Granting requires the signed-in
 * admin's `currentPassword` (the API rejects it with `UserCurrentPasswordIncorrect` otherwise);
 * revoking ignores it.
 */
export function setUserAdminRequest(
  id: string,
  isAdmin: boolean,
  currentPassword: string | null = null,
) {
  return patchApiUsersUserIdAdmin(id, { isAdmin, currentPassword })
}
