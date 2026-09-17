import { flushPromises } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { useRegistration } from '@/features/register'
import { createEmptyMinor } from '@/features/register/model/registration-form'
import type { RegisterResponse } from '@/shared/api/generated/models'

import { mountComposable } from '../../../../support/fixtures/auth-register/composable'
import { buildUserResponse } from '../../../../support/fixtures/user'
import { t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

type Registration = ReturnType<typeof useRegistration>

function useCooldownClock(): void {
  // Only the cooldown interval and clock are faked; TanStack Query and MSW keep real timeouts.
  vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] })
  vi.setSystemTime(new Date('2026-09-17T10:00:00Z'))
}

function serveRegister(response: RegisterResponse) {
  const bodies: unknown[] = []
  server.use(
    http.post('/api/auth/register', async ({ request }) => {
      bodies.push(await request.json())
      return HttpResponse.json(response, { status: 201 })
    }),
  )
  return bodies
}

function serveResend(response: () => Response | Promise<Response> = () => ok()) {
  const userIds: unknown[] = []
  server.use(
    http.post('/api/auth/:userId/resend-verification', ({ params }) => {
      userIds.push(params.userId)
      return response()
    }),
  )
  return userIds
}

function ok(): Response {
  return new HttpResponse(null, { status: 204 })
}

function fillAdult(registration: Registration): void {
  Object.assign(registration.form, {
    firstName: 'Ada',
    lastName: 'Lovelace',
    email: '  ada@example.test ',
    phone: '600000000',
    password: 'correct-horse-battery',
    confirmPassword: 'correct-horse-battery',
    dateOfBirth: '1990-05-10',
    gender: 'Female',
  })
}

async function registerSuccessfully(response: RegisterResponse) {
  serveRegister(response)
  const mounted = await mountComposable(() => useRegistration(), { attach: true })
  fillAdult(mounted.result)
  mounted.result.confirmAdult()
  mounted.result.submit()
  await vi.waitFor(() => expect(mounted.result.step.value).toBe('success'))
  return mounted
}

function notificationText(): string {
  return document.body.querySelector('.el-notification')?.textContent ?? ''
}

describe('useRegistration', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('starts at the age gate and moves between the gate and the form, scrolling to the top', async () => {
    const scrollTo = vi.spyOn(window, 'scrollTo')
    const { result } = await mountComposable(() => useRegistration())
    expect(result.step.value).toBe('age-gate')

    result.confirmAdult()
    expect(result.step.value).toBe('form')

    result.backToGate()
    expect(result.step.value).toBe('age-gate')
    expect(scrollTo).toHaveBeenCalledTimes(2)
  })

  it('submits the form and shows the success step with the trimmed email and minor count', async () => {
    const bodies = serveRegister({
      adult: buildUserResponse({ id: 'adult-1' }),
      minors: [buildUserResponse({ id: 'minor-1' })],
      requiresVerification: false,
    })
    const { result } = await mountComposable(() => useRegistration())
    fillAdult(result)
    result.form.minors.push({
      ...createEmptyMinor(),
      firstName: 'Byron',
      lastName: 'King',
      dateOfBirth: '2016-02-03',
      gender: 'Male',
    })

    result.submit()
    await vi.waitFor(() => expect(result.step.value).toBe('success'))

    expect(bodies).toHaveLength(1)
    expect(result.submittedEmail.value).toBe('ada@example.test')
    expect(result.submittedMinorCount.value).toBe(1)
    expect(result.requiresVerification.value).toBe(false)
    expect(result.resendCooldown.value).toBe(0)
  })

  it('falls back to the number of minors in the form when the response omits them', async () => {
    serveRegister({ adult: buildUserResponse({ id: 'adult-1' }), requiresVerification: false })
    const { result } = await mountComposable(() => useRegistration())
    fillAdult(result)
    result.form.minors.push(
      {
        ...createEmptyMinor(),
        firstName: 'A',
        lastName: 'K',
        dateOfBirth: '2016-01-01',
        gender: 'Male',
      },
      {
        ...createEmptyMinor(),
        firstName: 'B',
        lastName: 'K',
        dateOfBirth: '2017-01-01',
        gender: 'Other',
      },
    )

    result.submit()
    await vi.waitFor(() => expect(result.step.value).toBe('success'))

    expect(result.submittedMinorCount.value).toBe(2)
  })

  it('shows an error notification and stays on the form when registration fails', async () => {
    server.use(
      http.post('/api/auth/register', () => apiError(409, 'RegisterEmailOrPhoneAlreadyInUse')),
    )
    const { result } = await mountComposable(() => useRegistration(), { attach: true })
    fillAdult(result)
    result.confirmAdult()

    result.submit()
    await vi.waitFor(() =>
      expect(notificationText()).toContain(t('errors.RegisterEmailOrPhoneAlreadyInUse')),
    )

    expect(notificationText()).toContain(t('common.error'))
    expect(notificationText()).toContain('trace-123')
    expect(result.step.value).toBe('form')
  })

  it('locks resending for 60 seconds after a registration that needs verification', async () => {
    useCooldownClock()
    const userIds = serveResend()
    const { result } = await registerSuccessfully({
      adult: buildUserResponse({ id: 'adult-1' }),
      requiresVerification: true,
    })
    expect(result.requiresVerification.value).toBe(true)
    expect(result.resendCooldown.value).toBe(60)

    vi.advanceTimersByTime(1000)
    expect(result.resendCooldown.value).toBe(59)

    result.resend()
    await flushPromises()
    expect(userIds).toHaveLength(0)

    vi.advanceTimersByTime(59_000)
    expect(result.resendCooldown.value).toBe(0)
    expect(vi.getTimerCount()).toBe(0)
  })

  it('resends the verification email once the cooldown ends and restarts the cooldown', async () => {
    useCooldownClock()
    const userIds = serveResend()
    const { result } = await registerSuccessfully({
      adult: buildUserResponse({ id: 'adult-1' }),
      requiresVerification: true,
    })
    vi.advanceTimersByTime(60_000)

    result.resend()
    await vi.waitFor(() => expect(result.resendCooldown.value).toBe(60))

    expect(userIds).toEqual(['adult-1'])
    await vi.waitFor(() =>
      expect(notificationText()).toContain(t('features.register.toast.linkResentDetail')),
    )
    expect(notificationText()).toContain(t('features.register.toast.linkResentSummary'))
  })

  it('ignores resend requests while a resend is already in flight', async () => {
    useCooldownClock()
    let release: () => void = () => undefined
    const userIds = serveResend(
      () =>
        new Promise<Response>((resolve) => {
          release = () => resolve(ok())
        }),
    )
    const { result } = await registerSuccessfully({
      adult: buildUserResponse({ id: 'adult-1' }),
      requiresVerification: true,
    })
    vi.advanceTimersByTime(60_000)

    result.resend()
    await vi.waitFor(() => expect(result.isResending.value).toBe(true))
    await vi.waitFor(() => expect(userIds).toHaveLength(1))
    result.resend()
    release()
    await vi.waitFor(() => expect(result.isResending.value).toBe(false))

    expect(userIds).toHaveLength(1)
  })

  it('reports a failed resend without restarting the cooldown', async () => {
    useCooldownClock()
    serveResend(() => apiError(429, 'OtpResendCooldownActive'))
    const { result } = await registerSuccessfully({
      adult: buildUserResponse({ id: 'adult-1' }),
      requiresVerification: true,
    })
    vi.advanceTimersByTime(60_000)

    result.resend()
    await vi.waitFor(() =>
      expect(notificationText()).toContain(t('errors.OtpResendCooldownActive')),
    )

    expect(result.resendCooldown.value).toBe(0)
  })

  it('reports a generic error when resending without a created user id', async () => {
    const userIds = serveResend()
    const { result } = await registerSuccessfully({ requiresVerification: false })

    result.resend()
    await vi.waitFor(() => expect(notificationText()).toContain(t('errors.generic')))

    expect(userIds).toHaveLength(0)
  })

  it('resets the whole flow back to an empty age gate and stops the cooldown', async () => {
    useCooldownClock()
    const { result } = await registerSuccessfully({
      adult: buildUserResponse({ id: 'adult-1' }),
      minors: [buildUserResponse()],
      requiresVerification: true,
    })
    expect(vi.getTimerCount()).toBe(1)

    result.reset()

    expect(result.step.value).toBe('age-gate')
    expect(result.form.firstName).toBe('')
    expect(result.form.minors).toEqual([])
    expect(result.submittedEmail.value).toBe('')
    expect(result.submittedMinorCount.value).toBe(0)
    expect(result.requiresVerification.value).toBe(false)
    expect(result.resendCooldown.value).toBe(0)
    expect(result.isSubmitting.value).toBe(false)
    expect(vi.getTimerCount()).toBe(0)
  })

  it('stops the cooldown timer when the component unmounts', async () => {
    useCooldownClock()
    const { wrapper } = await registerSuccessfully({
      adult: buildUserResponse({ id: 'adult-1' }),
      requiresVerification: true,
    })
    expect(vi.getTimerCount()).toBe(1)

    wrapper.unmount()

    expect(vi.getTimerCount()).toBe(0)
  })
})
