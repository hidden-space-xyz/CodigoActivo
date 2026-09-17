import {
  getApiAuthMe,
  postApiAuthLogin,
  postApiAuthLogout,
} from '@/shared/api/generated/endpoints/auth/auth'
import { resetCsrfToken, unwrapOrNull } from '@/shared/api'

import type { Credentials } from '../model/credentials'
import type { AuthUser } from '../model/types'
import { toAuthUser } from './mapper'

/** Loads the signed-in user from the auth cookie; resolves to `null` on 401/403. */
export async function getCurrentUserRequest(): Promise<AuthUser | null> {
  const data = await unwrapOrNull(getApiAuthMe(), [401, 403])
  return data ? toAuthUser(data) : null
}

/** Signs in and resets the cached CSRF token so the next unsafe request fetches a fresh one. */
export async function loginRequest(credentials: Credentials): Promise<AuthUser> {
  const response = await postApiAuthLogin({
    identifier: credentials.identifier,
    password: credentials.password,
  })
  resetCsrfToken()
  return toAuthUser(response.data)
}

/** Signs out and resets the cached CSRF token even when the request fails. */
export async function logoutRequest(): Promise<void> {
  try {
    await postApiAuthLogout()
  } finally {
    resetCsrfToken()
  }
}
