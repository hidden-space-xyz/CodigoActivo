import type { AccountProfile } from '@/modules/account/domain/entities/account-profile.entity'
import type { AccountRepository } from '@/modules/account/domain/repositories/account-repository'

export function changeAccountRole(
  repository: AccountRepository,
  userId: string,
  roleId: string,
): Promise<AccountProfile> {
  return repository.changeRole(userId, roleId)
}
