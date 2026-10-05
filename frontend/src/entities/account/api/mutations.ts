import { mutationOptions } from '@tanstack/vue-query'

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
import { accountKeys } from './queries'
import {
  addAccountChildRequest,
  beginAuthenticatorSetupRequest,
  changeAccountPasswordRequest,
  confirmAuthenticatorRequest,
  confirmEmailChangeRequest,
  deleteAccountChildRequest,
  deleteAccountRequest,
  disableAuthenticatorRequest,
  forgotPasswordRequest,
  registerRequest,
  requestAccountDeletionCodeRequest,
  resendVerificationRequest,
  resetPasswordRequest,
  saveAccountEventRatingRequest,
  updateAccountChildRequest,
  updateAccountProfileRequest,
  verifyAccountRequest,
} from './requests'

/**
 * Mutation options of the account. Changes to the second factor and a confirmed email change
 * refresh the profile, changes to the household refresh the children and a rating refreshes the
 * history; the rest touch nothing cached, or leave it to the caller.
 */
export const accountMutations = {
  /** Saves the profile of `userId` and resolves to the stored one. */
  updateProfile: (userId: string) =>
    mutationOptions({
      mutationFn: (input: UpdateProfileInput) => updateAccountProfileRequest(userId, input),
    }),
  changePassword: (userId: string) =>
    mutationOptions({
      mutationFn: (input: ChangePasswordInput) => changeAccountPasswordRequest(userId, input),
    }),
  /** Starts enrolling an authenticator and resolves to the key to scan. */
  beginAuthenticatorSetup: () =>
    mutationOptions({
      mutationFn: (currentPassword: string) => beginAuthenticatorSetupRequest(currentPassword),
    }),
  confirmAuthenticator: () =>
    mutationOptions({
      mutationFn: (code: string) => confirmAuthenticatorRequest(code),
      meta: { invalidates: [accountKeys.profile()] },
    }),
  disableAuthenticator: () =>
    mutationOptions({
      mutationFn: (input: DisableAuthenticatorInput) => disableAuthenticatorRequest(input),
      meta: { invalidates: [accountKeys.profile()] },
    }),
  /** Registers a minor under `parentId`. */
  addChild: (parentId: string) =>
    mutationOptions({
      mutationFn: (input: MinorInput) => addAccountChildRequest(parentId, input),
      meta: { invalidates: [accountKeys.children()] },
    }),
  /** Updates a minor, keeping it under `parentId`. */
  updateChild: (parentId: string) =>
    mutationOptions({
      mutationFn: ({ childId, input }: { childId: string; input: MinorInput }) =>
        updateAccountChildRequest(childId, parentId, input),
      meta: { invalidates: [accountKeys.children()] },
    }),
  removeChild: () =>
    mutationOptions({
      mutationFn: (childId: string) => deleteAccountChildRequest(childId),
      meta: { invalidates: [accountKeys.children()] },
    }),
  requestDeletionCode: () =>
    mutationOptions({
      mutationFn: (currentPassword: string) => requestAccountDeletionCodeRequest(currentPassword),
    }),
  deleteAccount: () =>
    mutationOptions({
      mutationFn: (input: DeleteAccountInput) => deleteAccountRequest(input),
    }),
  rateEvent: () =>
    mutationOptions({
      mutationFn: ({ eventId, input }: { eventId: string; input: EventRatingInput }) =>
        saveAccountEventRatingRequest(eventId, input),
      meta: { invalidates: [accountKeys.history()] },
    }),
  /** Registers an adult with their minors; the API answers alike for a known email. */
  register: () =>
    mutationOptions({
      mutationFn: (input: RegistrationInput) => registerRequest(input),
    }),
  verify: () =>
    mutationOptions({
      mutationFn: ({ userId, otp }: { userId: string; otp: string }) =>
        verifyAccountRequest(userId, otp),
    }),
  /** Emails a new verification link to the pending account of an address, if there is one. */
  resendVerification: () =>
    mutationOptions({
      mutationFn: (email: string) => resendVerificationRequest(email),
    }),
  confirmEmailChange: () =>
    mutationOptions({
      mutationFn: ({ userId, code }: { userId: string; code: string }) =>
        confirmEmailChangeRequest(userId, code),
      meta: { invalidates: [accountKeys.profile()] },
    }),
  forgotPassword: () =>
    mutationOptions({
      mutationFn: (email: string) => forgotPasswordRequest(email),
    }),
  resetPassword: () =>
    mutationOptions({
      mutationFn: (input: ResetPasswordInput) => resetPasswordRequest(input),
    }),
}
