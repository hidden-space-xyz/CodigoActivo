import type { AuthRepository } from '@/modules/auth/domain/repositories/auth-repository'

export function logout(repository: AuthRepository): Promise<void> {
  return repository.logout()
}
