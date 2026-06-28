import type { RegistrationResult } from '@/modules/registration/domain/entities/registration-result.entity'
import type { RegistrationType } from '@/modules/registration/domain/entities/registration-type.entity'
import type { RegistrationForm } from '@/modules/registration/domain/value-objects/registration-form'

export interface RegistrationRepository {
  getRegistrationTypes(minor: boolean): Promise<readonly RegistrationType[]>
  register(form: RegistrationForm): Promise<RegistrationResult>
  verify(userId: string, otp: string): Promise<void>
}
