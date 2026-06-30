import {
  getApiAuthMe,
  postApiAuthLogin,
  postApiAuthLogout,
} from '@/shared/api/generated/endpoints/auth/auth'
import { ApiError, resetCsrfToken } from '@/shared/api'

import type { Credentials } from '../model/credentials'
import type { AuthUser } from '../model/types'
import { toAuthUser } from './mapper'

export async function getCurrentUserRequest(): Promise<AuthUser | null> {
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

export async function loginRequest(credentials: Credentials): Promise<AuthUser> {
  const response = await postApiAuthLogin({
    identifier: credentials.identifier,
    password: credentials.password,
  })
  resetCsrfToken()
  return toAuthUser(response.data)
}

export async function logoutRequest(): Promise<void> {
  try {
    await postApiAuthLogout()
  } finally {
    resetCsrfToken()
  }
}
