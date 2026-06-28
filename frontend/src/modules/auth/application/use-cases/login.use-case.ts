import type { AuthUser } from '@/modules/auth/domain/entities/auth-user.entity'
import type { AuthRepository } from '@/modules/auth/domain/repositories/auth-repository'
import type { Credentials } from '@/modules/auth/domain/value-objects/credentials'

export function login(repository: AuthRepository, credentials: Credentials): Promise<AuthUser> {
  return repository.login(credentials)
}
