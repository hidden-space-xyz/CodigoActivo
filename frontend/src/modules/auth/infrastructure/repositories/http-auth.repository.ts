import type { AuthUser } from '@/modules/auth/domain/entities/auth-user.entity'
import type { AuthRepository } from '@/modules/auth/domain/repositories/auth-repository'
import type { Credentials } from '@/modules/auth/domain/value-objects/credentials'
import { toAuthUser } from '@/modules/auth/infrastructure/mappers/auth-user.mapper'
import {
  getApiAuthMe,
  postApiAuthLogin,
  postApiAuthLogout,
} from '@/shared/api/generated/endpoints/auth/auth'
import { ApiError, resetCsrfToken } from '@/shared/api/http-client'

export class HttpAuthRepository implements AuthRepository {
  async getCurrentUser(): Promise<AuthUser | null> {
    try {
      const response = await getApiAuthMe()
      return toAuthUser(response.data)
    } catch (error) {
      if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
        return null
      }
      throw error
    }
  }

  async login(credentials: Credentials): Promise<AuthUser> {
    const response = await postApiAuthLogin({
      identifier: credentials.identifier,
      password: credentials.password,
    })
    resetCsrfToken()
    return toAuthUser(response.data)
  }

  async logout(): Promise<void> {
    try {
      await postApiAuthLogout()
    } finally {
      resetCsrfToken()
    }
  }
}
