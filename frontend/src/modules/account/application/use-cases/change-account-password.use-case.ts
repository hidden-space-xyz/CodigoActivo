import type { AccountRepository } from '@/modules/account/domain/repositories/account-repository'
import type { ChangePasswordInput } from '@/modules/account/domain/value-objects/account-inputs'

export function changeAccountPassword(
  repository: AccountRepository,
  userId: string,
  input: ChangePasswordInput,
): Promise<void> {
  return repository.changePassword(userId, input)
}
