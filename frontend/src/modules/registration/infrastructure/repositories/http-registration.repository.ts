import type { RegistrationResult } from '@/modules/registration/domain/entities/registration-result.entity'
import type { RegistrationType } from '@/modules/registration/domain/entities/registration-type.entity'
import type { RegistrationRepository } from '@/modules/registration/domain/repositories/registration-repository'
import type { RegistrationForm } from '@/modules/registration/domain/value-objects/registration-form'
import {
  toRegisterRequest,
  toRegistrationResult,
  toRegistrationType,
} from '@/modules/registration/infrastructure/mappers/registration.mapper'
import {
  patchApiAuthUserIdVerify,
  postApiAuthRegister,
} from '@/shared/api/generated/endpoints/auth/auth'
import { getApiUsersRegistrationTypes } from '@/shared/api/generated/endpoints/users/users'

export class HttpRegistrationRepository implements RegistrationRepository {
  async getRegistrationTypes(minor: boolean): Promise<readonly RegistrationType[]> {
    const response = await getApiUsersRegistrationTypes({ minor })
    return (response.data ?? []).map(toRegistrationType)
  }

  async register(form: RegistrationForm): Promise<RegistrationResult> {
    const response = await postApiAuthRegister(toRegisterRequest(form))
    return toRegistrationResult(response.data)
  }

  async verify(userId: string, otp: string): Promise<void> {
    await patchApiAuthUserIdVerify(userId, { otp })
  }
}
