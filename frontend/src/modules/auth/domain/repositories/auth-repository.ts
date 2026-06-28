import type { AuthUser } from '@/modules/auth/domain/entities/auth-user.entity'
import type { Credentials } from '@/modules/auth/domain/value-objects/credentials'

export interface AuthRepository {
  getCurrentUser(): Promise<AuthUser | null>
  login(credentials: Credentials): Promise<AuthUser>
  logout(): Promise<void>
}
