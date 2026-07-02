import { fetchODataList } from '@/shared/api'
import type { RegistrationTypeResponse } from '@/shared/api'
import {
  patchApiAuthUserIdVerify,
  postApiAuthRegister,
} from '@/shared/api/generated/endpoints/auth/auth'

import type { RegistrationForm } from '../model/registration-form'
import type { RegistrationResult, RegistrationType } from '../model/types'
import { toRegisterRequest, toRegistrationResult, toRegistrationType } from './mapper'

export async function getRegistrationTypesRequest(
  minor: boolean,
): Promise<readonly RegistrationType[]> {
  const { items } = await fetchODataList<RegistrationTypeResponse>('RegistrationTypes', {
    filter: minor ? 'isAllowedForMinors eq true' : 'isAllowedForAdults eq true',
    orderBy: 'name asc',
    top: 1000,
  })
  return items.map(toRegistrationType)
}

export async function registerRequest(form: RegistrationForm): Promise<RegistrationResult> {
  const response = await postApiAuthRegister(toRegisterRequest(form))
  return toRegistrationResult(response.data)
}

export async function verifyRegistrationRequest(userId: string, otp: string): Promise<void> {
  await patchApiAuthUserIdVerify(userId, { otp })
}
