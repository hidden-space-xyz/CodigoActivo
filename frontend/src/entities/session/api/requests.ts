import {
  getApiAuthMe,
  postApiAuthLogin,
  postApiAuthLogout,
} from '@/shared/api/generated/endpoints/auth/auth'
import { resetCsrfToken, unwrapOrNull } from '@/shared/api'

import type { Credentials } from '../model/credentials'
import type { AuthUser } from '../model/types'
import { toAuthUser } from './mapper'

export async function getCurrentUserRequest(): Promise<AuthUser | null> {
  const data = await unwrapOrNull(getApiAuthMe(), [401, 403])
  return data ? toAuthUser(data) : null
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
