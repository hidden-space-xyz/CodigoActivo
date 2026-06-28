import type { AccountChild } from '@/modules/account/domain/entities/account-child.entity'
import type { AccountRepository } from '@/modules/account/domain/repositories/account-repository'
import type { UpdateMinorInput } from '@/modules/account/domain/value-objects/account-inputs'

export function updateAccountChild(
  repository: AccountRepository,
  childId: string,
  parentId: string,
  input: UpdateMinorInput,
): Promise<AccountChild> {
  return repository.updateChild(childId, parentId, input)
}
