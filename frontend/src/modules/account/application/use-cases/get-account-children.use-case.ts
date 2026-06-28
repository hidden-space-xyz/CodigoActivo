import type { AccountChild } from '@/modules/account/domain/entities/account-child.entity'
import type { AccountRepository } from '@/modules/account/domain/repositories/account-repository'

export function getAccountChildren(
  repository: AccountRepository,
  parentId: string,
): Promise<readonly AccountChild[]> {
  return repository.getChildren(parentId)
}
