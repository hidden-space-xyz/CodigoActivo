import type { AccountRepository } from '@/modules/account/domain/repositories/account-repository'

export function deleteAccountChild(
  repository: AccountRepository,
  childId: string,
): Promise<void> {
  return repository.deleteChild(childId)
}
