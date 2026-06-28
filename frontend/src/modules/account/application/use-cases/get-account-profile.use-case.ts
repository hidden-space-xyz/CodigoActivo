import type { AccountProfile } from '@/modules/account/domain/entities/account-profile.entity'
import type { AccountRepository } from '@/modules/account/domain/repositories/account-repository'

export function getAccountProfile(repository: AccountRepository): Promise<AccountProfile | null> {
  return repository.getProfile()
}
