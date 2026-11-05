import {
  deleteApiUsersUserId,
  getApiUsers,
  getApiUsersStatusTypes,
  getApiUsersTypes,
  patchApiUsersUserIdChangeType,
  patchApiUsersUserIdStaffRole,
  postApiUsersUserIdPrivacyOfficer,
  postApiUsersUserIdTwoFactorReset,
} from '@/shared/api/generated/endpoints/users/users'
import { toPage } from '@/shared/api'
import type { ServerTablePage } from '@/shared/lib/paging'

import type { StaffRole } from '../model/staff-role'
import type { User, UserCatalogRef, UserListParams } from '../model/types'
import { toUser, toUserCatalogRef } from './mapper'

/** Fetches one page of the admin user list. */
export async function getUserListPageRequest(
  params: UserListParams,
): Promise<ServerTablePage<User>> {
  return toPage(await getApiUsers(params), toUser)
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

/**
 * Deletes a user together with their minors; only the superadministrator deletes other accounts,
 * and the API never deletes the superadministrator.
 */
export async function deleteUserRequest(id: string): Promise<void> {
  await deleteApiUsersUserId(id)
}

/** Assigns another user type. */
export async function changeUserTypeRequest(id: string, userTypeId: string): Promise<void> {
  await patchApiUsersUserIdChangeType(id, { userTypeId })
}

/**
 * Gives a user another cargo; only the superadministrator may, and never their own. Any cargo but
 * `None` requires the caller's `currentPassword` (the API rejects it with
 * `UserCurrentPasswordIncorrect` otherwise); removing the cargo ignores it.
 */
export async function setUserStaffRoleRequest(
  id: string,
  staffRole: StaffRole,
  currentPassword: string | null,
): Promise<void> {
  await patchApiUsersUserIdStaffRole(id, { staffRole, currentPassword })
}

/**
 * Designates a user as the data protection officer, which the account that held it stops being;
 * only the superadministrator may, with their `currentPassword`, and designating themselves takes
 * the designation back.
 */
export async function designatePrivacyOfficerRequest(
  id: string,
  currentPassword: string,
): Promise<void> {
  await postApiUsersUserIdPrivacyOfficer(id, { currentPassword })
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
