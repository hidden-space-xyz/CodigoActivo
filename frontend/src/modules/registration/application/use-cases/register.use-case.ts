import type { RegistrationResult } from '@/modules/registration/domain/entities/registration-result.entity'
import type { RegistrationRepository } from '@/modules/registration/domain/repositories/registration-repository'
import type { RegistrationForm } from '@/modules/registration/domain/value-objects/registration-form'

export function register(
  repository: RegistrationRepository,
  form: RegistrationForm,
): Promise<RegistrationResult> {
  return repository.register(form)
}
