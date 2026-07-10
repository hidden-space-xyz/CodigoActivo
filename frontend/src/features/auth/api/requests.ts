import {
  patchApiAuthUserIdResetPassword,
  postApiAuthForgotPassword,
} from '@/shared/api/generated/endpoints/auth/auth'

export async function forgotPasswordRequest(email: string): Promise<void> {
  await postApiAuthForgotPassword({ email })
}

export async function resetPasswordRequest(
  userId: string,
  otp: string,
  newPassword: string,
): Promise<void> {
  await patchApiAuthUserIdResetPassword(userId, { otp, newPassword })
}
