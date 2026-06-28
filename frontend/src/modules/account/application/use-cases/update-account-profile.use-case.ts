import type { AccountProfile } from '@/modules/account/domain/entities/account-profile.entity'
import type { AccountRepository } from '@/modules/account/domain/repositories/account-repository'
import type { UpdateProfileInput } from '@/modules/account/domain/value-objects/account-inputs'

export function updateAccountProfile(
  repository: AccountRepository,
  userId: string,
  input: UpdateProfileInput,
): Promise<AccountProfile> {
  return repository.updateProfile(userId, input)
}
