import {
  patchApiAuthUserIdVerify,
  postApiAuthRegister,
  postApiAuthUserIdResendVerification,
} from '@/shared/api/generated/endpoints/auth/auth'

import type { RegistrationForm } from '../model/registration-form'
import type { RegistrationResult } from '../model/types'
import { toRegisterRequest, toRegistrationResult } from './mapper'

/** Registers an adult together with any minors in one call (`POST /api/auth/register`). */
export async function registerRequest(form: RegistrationForm): Promise<RegistrationResult> {
  const response = await postApiAuthRegister(toRegisterRequest(form))
  return toRegistrationResult(response.data)
}

/** Activates an account with the one-time code from the verification email link. */
export async function verifyRegistrationRequest(userId: string, otp: string): Promise<void> {
  await patchApiAuthUserIdVerify(userId, { otp })
}

/** Asks the API to email the user a new account verification link. */
export async function resendVerificationRequest(userId: string): Promise<void> {
  await postApiAuthUserIdResendVerification(userId)
}
