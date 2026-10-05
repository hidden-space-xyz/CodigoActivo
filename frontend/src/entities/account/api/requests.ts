import {
  getApiAuthMe,
  patchApiAuthUserIdConfirmEmail,
  patchApiAuthUserIdResetPassword,
  patchApiAuthUserIdVerify,
  postApiAuthForgotPassword,
  postApiAuthRegister,
  postApiAuthResendVerification,
  postApiAuthTwoFactorAuthenticatorConfirm,
  postApiAuthTwoFactorAuthenticatorSetup,
  postApiAuthTwoFactorEmail,
} from '@/shared/api/generated/endpoints/auth/auth'
import { postApiEventsEventIdRating } from '@/shared/api/generated/endpoints/events/events'
import {
  getApiMeCertificates,
  getApiMeDeletion,
  getApiMeEventHistory,
  postApiMeDeletion,
  postApiMeDeletionCode,
} from '@/shared/api/generated/endpoints/me/me'
import {
  deleteApiUsersUserId,
  getApiUsers,
  patchApiUsersUserIdPassword,
  postApiUsersUserIdChildren,
  putApiUsersUserId,
} from '@/shared/api/generated/endpoints/users/users'
import { toPage, unwrapOrNull } from '@/shared/api'

import type {
  ChangePasswordInput,
  DeleteAccountInput,
  DisableAuthenticatorInput,
  EventRatingInput,
  MinorInput,
  RegistrationInput,
  ResetPasswordInput,
  UpdateProfileInput,
} from '../model/account-inputs'
import type {
  AccountCertificate,
  AccountChild,
  AccountHistoryEntry,
  AccountProfile,
  AuthenticatorSetup,
} from '../model/types'
import {
  toAccountCertificate,
  toAccountChild,
  toAccountHistoryEntry,
  toAccountProfile,
  toAuthenticatorSetup,
  toMinorRequest,
  toRegisterRequest,
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
  const response = await getApiUsers({ parentId, pageSize: 100, sort: 'firstName' })
  return toPage(response).items.map(toAccountChild)
}

/** Saves the user's own profile and returns the updated profile from the server. */
export async function updateAccountProfileRequest(
  userId: string,
  input: UpdateProfileInput,
): Promise<AccountProfile> {
  const response = await putApiUsersUserId(userId, toUpdateProfileRequest(input))
  return toAccountProfile(response.data)
}

/**
 * Asks the API whether the signed-in user may delete their own account (`GET /api/me/deletion`);
 * only the initial administrator may not.
 */
export async function getAccountDeletionAllowedRequest(): Promise<boolean> {
  const { data } = await getApiMeDeletion()
  return data.allowed
}

/**
 * Asks the API to email the confirmation code that authorizes deleting the own account
 * (`POST /api/me/deletion/code`). The password is verified first and nothing is deleted yet; users
 * with an authenticator application get no email and read the code from their app instead.
 */
export async function requestAccountDeletionCodeRequest(currentPassword: string): Promise<void> {
  await postApiMeDeletionCode({ currentPassword })
}

/**
 * Deletes the signed-in user's own account with the password and the second-factor code
 * (`POST /api/me/deletion`). The API also removes the minors under their guardianship and ends
 * the session.
 */
export async function deleteAccountRequest(input: DeleteAccountInput): Promise<void> {
  await postApiMeDeletion({ currentPassword: input.currentPassword, code: input.code })
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

/**
 * Starts enrolling an authenticator application (`POST /api/auth/two-factor/authenticator/setup`).
 * The API verifies `currentPassword` and answers with the key to scan or type; nothing changes
 * until `confirmAuthenticatorRequest` succeeds.
 */
export async function beginAuthenticatorSetupRequest(
  currentPassword: string,
): Promise<AuthenticatorSetup> {
  const response = await postApiAuthTwoFactorAuthenticatorSetup({ currentPassword })
  return toAuthenticatorSetup(response.data)
}

/** Confirms the pending enrollment with the first code the application generated. */
export async function confirmAuthenticatorRequest(code: string): Promise<void> {
  await postApiAuthTwoFactorAuthenticatorConfirm({ code })
}

/** Removes the authenticator so login codes arrive by email again (`POST /api/auth/two-factor/email`). */
export async function disableAuthenticatorRequest(input: DisableAuthenticatorInput): Promise<void> {
  await postApiAuthTwoFactorEmail({ currentPassword: input.currentPassword, code: input.code })
}

/** Registers a minor under `parentId` and returns the created child. */
export async function addAccountChildRequest(
  parentId: string,
  input: MinorInput,
): Promise<AccountChild> {
  const response = await postApiUsersUserIdChildren(parentId, toMinorRequest(input))
  return toAccountChild(response.data)
}

/** Updates a minor through the generic user endpoint, re-sending `parentId` to keep the link. */
export async function updateAccountChildRequest(
  childId: string,
  parentId: string,
  input: MinorInput,
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
  return data.map(toAccountHistoryEntry)
}

/** Loads the participation certificates available to the user and their minors. */
export async function getAccountCertificatesRequest(): Promise<readonly AccountCertificate[]> {
  const { data } = await getApiMeCertificates()
  return data.map(toAccountCertificate)
}

/**
 * Submits an anonymous rating for an event (`POST`). Nothing links it to the account, so it cannot
 * be edited afterwards and every accepted call adds another rating.
 */
export async function saveAccountEventRatingRequest(
  eventId: string,
  input: EventRatingInput,
): Promise<void> {
  await postApiEventsEventIdRating(eventId, toSaveEventRatingRequest(input))
}

/**
 * Registers an adult together with any minors in one call (`POST /api/auth/register`). The API
 * answers alike whether or not the email already has an account, so nothing about the account
 * comes back; the mail sent to the address tells its owner how to go on.
 */
export async function registerRequest(input: RegistrationInput): Promise<void> {
  await postApiAuthRegister(toRegisterRequest(input))
}

/** Activates an account with the one-time code from the verification email link. */
export async function verifyAccountRequest(userId: string, otp: string): Promise<void> {
  await patchApiAuthUserIdVerify(userId, { otp })
}

/**
 * Asks the API to email a new verification link to the account that waits for verification with
 * `email` (`POST /api/auth/resend-verification`). It succeeds alike whether or not such an account
 * exists.
 */
export async function resendVerificationRequest(email: string): Promise<void> {
  await postApiAuthResendVerification({ email })
}

/**
 * Moves the account to the new email its holder asked for, with the code from the link emailed to
 * that address (`PATCH /api/auth/{userId}/confirm-email`). It needs no session.
 */
export async function confirmEmailChangeRequest(userId: string, code: string): Promise<void> {
  await patchApiAuthUserIdConfirmEmail(userId, { otp: code })
}

/** Asks the API (`POST /api/auth/forgot-password`) to email a password reset link. */
export async function forgotPasswordRequest(email: string): Promise<void> {
  await postApiAuthForgotPassword({ email })
}

/**
 * Sets a new password with the one-time code from the reset link
 * (`PATCH /api/auth/{userId}/reset-password`).
 */
export async function resetPasswordRequest(input: ResetPasswordInput): Promise<void> {
  await patchApiAuthUserIdResetPassword(input.userId, {
    otp: input.otp,
    newPassword: input.newPassword,
  })
}
