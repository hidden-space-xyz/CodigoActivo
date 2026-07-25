import { getApiAuthMe } from '@/shared/api/generated/endpoints/auth/auth'
import { putApiEventsEventIdRating } from '@/shared/api/generated/endpoints/events/events'
import { getApiMeEventHistory } from '@/shared/api/generated/endpoints/me/me'
import {
  deleteApiUsersUserId,
  getApiUsers,
  patchApiUsersUserIdPassword,
  postApiUsersUserIdChildren,
  putApiUsersUserId,
} from '@/shared/api/generated/endpoints/users/users'
import { ApiError, toPage } from '@/shared/api'

import type {
  AddMinorInput,
  ChangePasswordInput,
  EventRatingInput,
  UpdateMinorInput,
  UpdateProfileInput,
} from '../model/account-inputs'
import type {
  AccountChild,
  AccountEventRating,
  AccountHistoryEntry,
  AccountProfile,
} from '../model/types'
import {
  toAccountChild,
  toAccountEventRating,
  toAccountHistoryEntry,
  toAccountProfile,
  toAddMinorRequest,
  toSaveEventRatingRequest,
  toUpdateMinorRequest,
  toUpdateProfileRequest,
} from './mapper'

export async function getAccountProfileRequest(): Promise<AccountProfile | null> {
  try {
    const response = await getApiAuthMe()
    return toAccountProfile(response.data)
  } catch (error) {
    if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
      return null
    }
    throw error
  }
}

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

export async function updateAccountProfileRequest(
  userId: string,
  input: UpdateProfileInput,
): Promise<AccountProfile> {
  const response = await putApiUsersUserId(userId, toUpdateProfileRequest(input))
  return toAccountProfile(response.data)
}

export async function deleteAccountRequest(userId: string): Promise<void> {
  await deleteApiUsersUserId(userId)
}

export async function changeAccountPasswordRequest(
  userId: string,
  input: ChangePasswordInput,
): Promise<void> {
  await patchApiUsersUserIdPassword(userId, {
    currentPassword: input.currentPassword,
    newPassword: input.newPassword,
  })
}

export async function addAccountChildRequest(
  parentId: string,
  input: AddMinorInput,
): Promise<AccountChild> {
  const response = await postApiUsersUserIdChildren(parentId, toAddMinorRequest(input))
  return toAccountChild(response.data)
}

export async function updateAccountChildRequest(
  childId: string,
  parentId: string,
  input: UpdateMinorInput,
): Promise<AccountChild> {
  const response = await putApiUsersUserId(childId, toUpdateMinorRequest(input, parentId))
  return toAccountChild(response.data)
}

export async function deleteAccountChildRequest(childId: string): Promise<void> {
  await deleteApiUsersUserId(childId)
}

export async function getAccountHistoryRequest(): Promise<readonly AccountHistoryEntry[]> {
  const { data } = await getApiMeEventHistory()
  return (data ?? []).map(toAccountHistoryEntry)
}

export async function saveAccountEventRatingRequest(
  eventId: string,
  input: EventRatingInput,
): Promise<AccountEventRating> {
  const response = await putApiEventsEventIdRating(eventId, toSaveEventRatingRequest(input))
  return toAccountEventRating(response.data)
}
