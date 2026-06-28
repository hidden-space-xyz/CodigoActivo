import type { RegistrationType } from '@/modules/account/domain/entities/registration-type.entity'
import type { AccountRepository } from '@/modules/account/domain/repositories/account-repository'

export function getRegistrationTypes(
  repository: AccountRepository,
  minor: boolean,
): Promise<readonly RegistrationType[]> {
  return repository.getRegistrationTypes(minor)
}
