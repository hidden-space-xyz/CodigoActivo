import { flushPromises } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { useSession } from '@/entities/session'
import { useTwoFactorLogin } from '@/features/auth'

import { mountComposable } from '../../../../support/fixtures/auth-register/composable'
import { buildLoginChallenge, buildUserResponse } from '../../../../support/fixtures/user'
import { t } from '../../../../support/render'
import { apiError, http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../../../support/server'

function useCooldownClock(): void {
  // Only the cooldown interval and clock are faked; TanStack Query and MSW keep real timeouts.
  vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] })
  vi.setSystemTime(new Date('2026-09-17T10:00:00Z'))
}

function serveChallenge(response: () => Response = () => HttpResponse.json(buildLoginChallenge())) {
  server.use(http.get('/api/auth/login/two-factor', response))
}

function serveVerify(response: () => Response = () => HttpResponse.json(buildUserResponse())) {
  const received: { body: unknown; csrf: string | null }[] = []
  server.use(
    http.post('/api/auth/login/two-factor', async ({ request }) => {
      received.push({ body: await request.json(), csrf: request.headers.get('X-CSRF-TOKEN') })
      return response()
    }),
  )
  return received
}

function serveResend(response: () => Response = () => new HttpResponse(null, { status: 204 })) {
  const calls = { count: 0 }
  server.use(
    http.post('/api/auth/login/two-factor/resend', () => {
      calls.count += 1
      return response()
    }),
  )
  return calls
}

function notificationText(): string {
  return document.body.querySelector('.el-notification')?.textContent ?? ''
}

async function mountReady(route = '/login/verify') {
  const mounted = await mountComposable(() => useTwoFactorLogin(), { route, attach: true })
  await vi.waitFor(() => expect(mounted.result.isLoading.value).toBe(false))
  return mounted
}

describe('useTwoFactorLogin', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('loads the pending email challenge and starts the resend cooldown', async () => {
    useCooldownClock()
    serveChallenge()

    const { result } = await mountReady()

    expect(result.method.value).toBe('Email')
    expect(result.maskedEmail.value).toBe('a***@example.test')
    expect(result.expired.value).toBe(false)
    expect(result.resendCooldown.value).toBe(60)

    vi.advanceTimersByTime(60_000)
    expect(result.resendCooldown.value).toBe(0)
  })

  it('loads an authenticator challenge without a masked address or cooldown', async () => {
    serveChallenge(() =>
      HttpResponse.json(buildLoginChallenge({ method: 'Authenticator', maskedEmail: null })),
    )

    const { result } = await mountReady()

    expect(result.method.value).toBe('Authenticator')
    expect(result.maskedEmail.value).toBeNull()
    expect(result.resendCooldown.value).toBe(0)
  })

  it('reports an expired challenge when the API has none', async () => {
    serveChallenge(() => apiError(401, 'TwoFactorChallengeExpired'))

    const { result } = await mountReady('/login/verify?redirect=/events')

    expect(result.expired.value).toBe(true)
    expect(result.loginRoute.value).toEqual({ name: 'login', query: { redirect: '/events' } })
  })

  it('treats a failed challenge lookup as expired so the user can log in again', async () => {
    serveChallenge(() => apiError(500))

    const { result } = await mountReady()

    expect(result.expired.value).toBe(true)
  })

  it('verifies the trimmed code, stores the user and navigates to the redirect', async () => {
    serveChallenge()
    const received = serveVerify(() => HttpResponse.json(buildUserResponse({ firstName: 'Grace' })))
    const { result, router } = await mountReady('/login/verify?redirect=/events')
    result.form.code = ' 123456 '

    result.submit()
    await vi.waitFor(() => expect(router.currentRoute.value.fullPath).toBe('/events'))

    expect(received).toEqual([{ body: { code: '123456' }, csrf: TEST_CSRF_TOKEN }])
    expect(useSession().displayName).toBe('Grace')
  })

  it('goes home after verifying without a redirect', async () => {
    serveChallenge()
    serveVerify()
    const { result, router } = await mountReady()
    result.form.code = '123456'

    result.submit()
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('home'))

    expect(result.loginRoute.value).toEqual({ name: 'login' })
  })

  it('does not call the API for a blank code', async () => {
    serveChallenge()
    const received = serveVerify()
    const { result } = await mountReady()
    result.form.code = '   '

    result.submit()
    await flushPromises()

    expect(received).toHaveLength(0)
    expect(result.errorMessage.value).toBeNull()
  })

  it('shows the localized error for a wrong code and clears it on the next attempt', async () => {
    serveChallenge()
    serveVerify(() => apiError(400, 'TwoFactorCodeInvalid'))
    const { result } = await mountReady()
    result.form.code = '000000'

    result.submit()
    await vi.waitFor(() => expect(result.errorMessage.value).toBe(t('errors.TwoFactorCodeInvalid')))
    expect(useSession().isAuthenticated).toBe(false)

    result.submit()
    expect(result.errorMessage.value).toBeNull()
  })

  it('marks the challenge as expired when verification says so', async () => {
    serveChallenge()
    serveVerify(() => apiError(401, 'TwoFactorChallengeExpired'))
    const { result } = await mountReady()
    result.form.code = '123456'

    result.submit()
    await vi.waitFor(() => expect(result.expired.value).toBe(true))

    expect(result.errorMessage.value).toBeNull()
  })

  it('resends the code once the cooldown ends, restarts it and confirms with a toast', async () => {
    useCooldownClock()
    serveChallenge()
    const calls = serveResend()
    const { result } = await mountReady()

    result.resendCode()
    await flushPromises()
    expect(calls.count).toBe(0)

    vi.advanceTimersByTime(60_000)
    expect(result.resendCooldown.value).toBe(0)
    result.resendCode()
    await vi.waitFor(() => expect(calls.count).toBe(1))
    await vi.waitFor(() =>
      expect(notificationText()).toContain(t('features.auth.twoFactor.codeResentDetail')),
    )
    expect(result.resendCooldown.value).toBe(60)
  })

  it('reports resend failures as a toast, or as an expired challenge', async () => {
    useCooldownClock()
    serveChallenge()
    const { result } = await mountReady()
    vi.advanceTimersByTime(60_000)

    serveResend(() => apiError(409, 'TwoFactorResendCooldownActive'))
    result.resendCode()
    await vi.waitFor(() =>
      expect(notificationText()).toContain(t('errors.TwoFactorResendCooldownActive')),
    )
    expect(result.expired.value).toBe(false)

    serveResend(() => apiError(401, 'TwoFactorChallengeExpired'))
    result.resendCode()
    await vi.waitFor(() => expect(result.expired.value).toBe(true))
  })
})
