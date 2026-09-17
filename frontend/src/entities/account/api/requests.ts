import { getApiAuthMe } from '@/shared/api/generated/endpoints/auth/auth'
import { putApiEventsEventIdRating } from '@/shared/api/generated/endpoints/events/events'
import { getApiMeCertificates, getApiMeEventHistory } from '@/shared/api/generated/endpoints/me/me'
import {
  deleteApiUsersUserId,
  getApiUsers,
  patchApiUsersUserIdPassword,
  postApiUsersUserIdChildren,
  putApiUsersUserId,
} from '@/shared/api/generated/endpoints/users/users'
import { toPage, unwrapOrNull } from '@/shared/api'

import type {
  AddMinorInput,
  ChangePasswordInput,
  EventRatingInput,
  UpdateMinorInput,
  UpdateProfileInput,
} from '../model/account-inputs'
import type {
  AccountChild,
  AccountCertificate,
  AccountEventRating,
  AccountHistoryEntry,
  AccountProfile,
} from '../model/types'
import {
  toAccountChild,
  toAccountCertificate,
  toAccountEventRating,
  toAccountHistoryEntry,
  toAccountProfile,
  toAddMinorRequest,
  toSaveEventRatingRequest,
  toUpdateMinorRequest,
  toUpdateProfileRequest,
} from './mapper'

/** Loads the signed-in user from `/api/auth/me`; resolves `null` on 401/403 (no valid session). */
export async function getAccountProfileRequest(): Promise<AccountProfile | null> {
  const data = await unwrapOrNull(getApiAuthMe(), [401, 403])
  return data ? toAccountProfile(data) : null
}

/** Lists the minors registered under `parentId`, sorted by first name (first 100 only). */
export async function getAccountChildrenRequest(
  parentId: string,
): Promise<readonly AccountChild[]> {
  const { items } = await getApiUsers({
    parentId,
    pageSize: 100,
    sort: 'firstName',
  }).then(toPage)
  return items.map(toAccountChild)
}

/** Saves the user's own profile and returns the updated profile from the server. */
export async function updateAccountProfileRequest(
  userId: string,
  input: UpdateProfileInput,
): Promise<AccountProfile> {
  const response = await putApiUsersUserId(userId, toUpdateProfileRequest(input))
  return toAccountProfile(response.data)
}

/** Deletes the user account identified by `userId`. */
export async function deleteAccountRequest(userId: string): Promise<void> {
  await deleteApiUsersUserId(userId)
}

/** Changes the password; the API verifies `currentPassword` before applying the new one. */
export async function changeAccountPasswordRequest(
  userId: string,
  input: ChangePasswordInput,
): Promise<void> {
  await patchApiUsersUserIdPassword(userId, {
    currentPassword: input.currentPassword,
    newPassword: input.newPassword,
  })
}

/** Registers a minor under `parentId` and returns the created child. */
export async function addAccountChildRequest(
  parentId: string,
  input: AddMinorInput,
): Promise<AccountChild> {
  const response = await postApiUsersUserIdChildren(parentId, toAddMinorRequest(input))
  return toAccountChild(response.data)
}

/** Updates a minor through the generic user endpoint, re-sending `parentId` to keep the link. */
export async function updateAccountChildRequest(
  childId: string,
  parentId: string,
  input: UpdateMinorInput,
): Promise<AccountChild> {
  const response = await putApiUsersUserId(childId, toUpdateMinorRequest(input, parentId))
  return toAccountChild(response.data)
}

/** Deletes a minor's user record. */
export async function deleteAccountChildRequest(childId: string): Promise<void> {
  await deleteApiUsersUserId(childId)
}

/** Loads the events the user or their minors took part in, from `/api/me/event-history`. */
export async function getAccountHistoryRequest(): Promise<readonly AccountHistoryEntry[]> {
  const { data } = await getApiMeEventHistory()
  return (data ?? []).map(toAccountHistoryEntry)
}

/** Loads the participation certificates available to the user and their minors. */
export async function getAccountCertificatesRequest(): Promise<readonly AccountCertificate[]> {
  const { data } = await getApiMeCertificates()
  return (data ?? []).map(toAccountCertificate)
}

/** Creates or replaces the user's rating for an event (`PUT`) and returns the stored rating. */
export async function saveAccountEventRatingRequest(
  eventId: string,
  input: EventRatingInput,
): Promise<AccountEventRating> {
  const response = await putApiEventsEventIdRating(eventId, toSaveEventRatingRequest(input))
  return toAccountEventRating(response.data)
}
