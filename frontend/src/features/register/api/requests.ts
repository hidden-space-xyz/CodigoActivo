import {
  patchApiAuthUserIdVerify,
  postApiAuthRegister,
} from '@/shared/api/generated/endpoints/auth/auth'

import type { RegistrationForm } from '../model/registration-form'
import type { RegistrationResult } from '../model/types'
import { toRegisterRequest, toRegistrationResult } from './mapper'

export async function registerRequest(form: RegistrationForm): Promise<RegistrationResult> {
  const response = await postApiAuthRegister(toRegisterRequest(form))
  return toRegistrationResult(response.data)
}

export async function verifyRegistrationRequest(userId: string, otp: string): Promise<void> {
  await patchApiAuthUserIdVerify(userId, { otp })
}
