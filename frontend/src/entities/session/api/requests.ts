import {
  getApiAuthLoginTwoFactor,
  getApiAuthMe,
  postApiAuthLogin,
  postApiAuthLoginTwoFactor,
  postApiAuthLoginTwoFactorResend,
  postApiAuthLogout,
} from '@/shared/api/generated/endpoints/auth/auth'
import { resetCsrfToken, unwrapOrNull } from '@/shared/api'

import type { Credentials } from '../model/credentials'
import type { AuthUser, LoginChallenge } from '../model/types'
import { toAuthUser, toLoginChallenge } from './mapper'

/** Loads the signed-in user from the auth cookie; resolves to `null` on 401/403. */
export async function getCurrentUserRequest(): Promise<AuthUser | null> {
  const data = await unwrapOrNull(getApiAuthMe(), [401, 403])
  return data ? toAuthUser(data) : null
}

/**
 * Runs the password step of the login (`POST /api/auth/login`). A correct password never signs
 * the user in by itself: the API answers with the second factor it now expects.
 */
export async function loginRequest(credentials: Credentials): Promise<LoginChallenge> {
  const response = await postApiAuthLogin({
    identifier: credentials.identifier,
    password: credentials.password,
  })
  return toLoginChallenge(response.data)
}

/**
 * Describes the pending second-factor challenge of the browser
 * (`GET /api/auth/login/two-factor`); resolves to `null` when there is none or it expired.
 */
export async function getLoginChallengeRequest(): Promise<LoginChallenge | null> {
  const data = await unwrapOrNull(getApiAuthLoginTwoFactor(), [401])
  return data ? toLoginChallenge(data) : null
}

/**
 * Presents the second factor (`POST /api/auth/login/two-factor`). On success the session cookie
 * is set, so the cached CSRF token is dropped for the next unsafe request. The cookie outlives the
 * browser session only when `keepSignedIn` carries the user's explicit request.
 */
export async function verifyTwoFactorLoginRequest(
  code: string,
  keepSignedIn: boolean,
): Promise<AuthUser> {
  const response = await postApiAuthLoginTwoFactor({ code, keepSignedIn })
  resetCsrfToken()
  return toAuthUser(response.data)
}

/** Asks the API to email a new code for the pending challenge. */
export async function resendTwoFactorCodeRequest(): Promise<void> {
  await postApiAuthLoginTwoFactorResend()
}

/** Signs out and resets the cached CSRF token even when the request fails. */
export async function logoutRequest(): Promise<void> {
  try {
    await postApiAuthLogout()
  } finally {
    resetCsrfToken()
  }
}
