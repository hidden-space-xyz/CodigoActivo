import type { AccountChild } from '@/modules/account/domain/entities/account-child.entity'
import type { AccountRepository } from '@/modules/account/domain/repositories/account-repository'

export function changeChildRole(
  repository: AccountRepository,
  childId: string,
  roleId: string,
): Promise<AccountChild> {
  return repository.changeChildRole(childId, roleId)
}
