import type { AuthUser } from '@/modules/auth/domain/entities/auth-user.entity'
import type { AuthRepository } from '@/modules/auth/domain/repositories/auth-repository'

export function getCurrentUser(repository: AuthRepository): Promise<AuthUser | null> {
  return repository.getCurrentUser()
}
