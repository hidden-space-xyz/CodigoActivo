import type { RegistrationType } from '@/modules/registration/domain/entities/registration-type.entity'
import type { RegistrationRepository } from '@/modules/registration/domain/repositories/registration-repository'

export function getRegistrationTypes(
  repository: RegistrationRepository,
  minor: boolean,
): Promise<readonly RegistrationType[]> {
  return repository.getRegistrationTypes(minor)
}
