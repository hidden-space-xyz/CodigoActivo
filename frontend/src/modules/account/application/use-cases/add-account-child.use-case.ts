import type { AccountChild } from '@/modules/account/domain/entities/account-child.entity'
import type { AccountRepository } from '@/modules/account/domain/repositories/account-repository'
import type { AddMinorInput } from '@/modules/account/domain/value-objects/account-inputs'

export function addAccountChild(
  repository: AccountRepository,
  parentId: string,
  input: AddMinorInput,
): Promise<AccountChild> {
  return repository.addChild(parentId, input)
}
