import {
  patchApiAuthUserIdResetPassword,
  postApiAuthForgotPassword,
} from '@/shared/api/generated/endpoints/auth/auth'

/** Asks the API (`POST /api/auth/forgot-password`) to email a password reset link. */
export async function forgotPasswordRequest(email: string): Promise<void> {
  await postApiAuthForgotPassword({ email })
}

/**
 * Sets a new password with the one-time code from the reset link
 * (`PATCH /api/auth/{userId}/reset-password`).
 */
export async function resetPasswordRequest(
  userId: string,
  otp: string,
  newPassword: string,
): Promise<void> {
  await patchApiAuthUserIdResetPassword(userId, { otp, newPassword })
}
