import {
  deleteApiUsersUserId,
  getApiUsers,
  getApiUsersStatusTypes,
  getApiUsersTypes,
  getApiUsersUserId,
  patchApiUsersUserIdAdmin,
  patchApiUsersUserIdChangeType,
  postApiUsersUserIdTwoFactorReset,
  putApiUsersUserId,
} from '@/shared/api/generated/endpoints/users/users'
import { toPage } from '@/shared/api'
import type { ServerTablePage } from '@/shared/lib/paging'

import type { UpdateUserInput, User, UserCatalogRef, UserListParams } from '../model/types'
import { toUpdateUserRequest, toUser, toUserCatalogRef } from './mapper'

/** Fetches one page of the admin user list. */
export async function getUserListPageRequest(
  params: UserListParams,
): Promise<ServerTablePage<User>> {
  const { items, total } = toPage(await getApiUsers(params))
  return { items: items.map(toUser), total }
}

/** Loads a user by id; unlike other detail requests a 404 throws instead of resolving `null`. */
export async function getUserRequest(id: string): Promise<User> {
  const response = await getApiUsersUserId(id)
  return toUser(response.data)
}

/** Lists the user types an admin can assign. */
export async function getUserTypesRequest(): Promise<readonly UserCatalogRef[]> {
  const response = await getApiUsersTypes()
  return response.data.map(toUserCatalogRef)
}

/** Lists the account statuses users are filtered by. */
export async function getUserStatusesRequest(): Promise<readonly UserCatalogRef[]> {
  const response = await getApiUsersStatusTypes()
  return response.data.map(toUserCatalogRef)
}

/** Replaces a user's data. */
export async function updateUserRequest(id: string, input: UpdateUserInput): Promise<void> {
  await putApiUsersUserId(id, toUpdateUserRequest(input))
}

/** Deletes a user. */
export async function deleteUserRequest(id: string): Promise<void> {
  await deleteApiUsersUserId(id)
}

/** Assigns another user type. */
export async function changeUserTypeRequest(id: string, userTypeId: string): Promise<void> {
  await patchApiUsersUserIdChangeType(id, { userTypeId })
}

/**
 * Grants or revokes the admin role. Granting requires the signed-in admin's `currentPassword` (the
 * API rejects it with `UserCurrentPasswordIncorrect` otherwise); revoking ignores it.
 */
export async function setUserAdminRequest(
  id: string,
  isAdmin: boolean,
  currentPassword: string | null,
): Promise<void> {
  await patchApiUsersUserIdAdmin(id, { isAdmin, currentPassword })
}

/**
 * Returns a user's second factor to email and clears any lockout; the signed-in admin's
 * `currentPassword` authorizes it.
 */
export async function resetUserTwoFactorRequest(
  id: string,
  currentPassword: string,
): Promise<void> {
  await postApiUsersUserIdTwoFactorReset(id, { currentPassword })
}
