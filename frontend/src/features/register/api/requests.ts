import {
  patchApiAuthUserIdVerify,
  postApiAuthRegister,
} from '@/shared/api/generated/endpoints/auth/auth'
import { getApiUsersRegistrationTypes } from '@/shared/api/generated/endpoints/users/users'

import type { RegistrationForm } from '../model/registration-form'
import type { RegistrationResult, RegistrationType } from '../model/types'
import { toRegisterRequest, toRegistrationResult, toRegistrationType } from './mapper'

export async function getRegistrationTypesRequest(
  minor: boolean,
): Promise<readonly RegistrationType[]> {
  const response = await getApiUsersRegistrationTypes({ minor })
  return (response.data ?? []).map(toRegistrationType)
}

export async function registerRequest(form: RegistrationForm): Promise<RegistrationResult> {
  const response = await postApiAuthRegister(toRegisterRequest(form))
  return toRegistrationResult(response.data)
}

export async function verifyRegistrationRequest(userId: string, otp: string): Promise<void> {
  await patchApiAuthUserIdVerify(userId, { otp })
}
