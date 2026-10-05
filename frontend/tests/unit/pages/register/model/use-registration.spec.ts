import { flushPromises } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { useRegistration } from '@/pages/register/model/use-registration'
import { createEmptyMinor } from '@/pages/register/model/registration-form'

import { t } from '../../../../support/render'
import { apiError, http, noContent, server } from '../../../../support/server'
import { mountComposable } from '../../../../support/render'

type Registration = ReturnType<typeof useRegistration>

function useCooldownClock(): void {
  // Only the cooldown interval and clock are faked; TanStack Query and MSW keep real timeouts.
  vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] })
  vi.setSystemTime(new Date('2026-09-17T10:00:00Z'))
}

function serveRegister() {
  const bodies: unknown[] = []
  server.use(
    http.post('/api/auth/register', async ({ request }) => {
      bodies.push(await request.json())
      return noContent()
    }),
  )
  return bodies
}

function serveResend(response: () => Response | Promise<Response> = noContent) {
  const emails: unknown[] = []
  server.use(
    http.post('/api/auth/resend-verification', async ({ request }) => {
      emails.push(((await request.json()) as { email: unknown }).email)
      return response()
    }),
  )
  return emails
}

function fillAdult(registration: Registration): void {
  Object.assign(registration.form, {
    firstName: 'Ada',
    lastName: 'Lovelace',
    email: '  ada@example.test ',
    phone: '600000000',
    password: 'correct-horse-battery',
    confirmPassword: 'correct-horse-battery',
    nationalId: '12345678Z',
    gender: 'Female',
  })
}

async function registerSuccessfully() {
  serveRegister()
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

  it('submits the form and shows the success step with the submitted email and minors', async () => {
    const bodies = serveRegister()
    const { result } = await mountComposable(() => useRegistration())
    fillAdult(result)
    result.form.minors.push({
      ...createEmptyMinor(),
      firstName: 'Byron',
      lastName: 'King',
      birthDate: '2016-02-03',
      gender: 'Male',
    })

    result.submit()
    await vi.waitFor(() => expect(result.step.value).toBe('success'))

    expect(bodies).toHaveLength(1)
    expect(result.submittedEmail.value).toBe('ada@example.test')
    expect(result.submittedMinorCount.value).toBe(1)
    expect(result.resendCooldown.value).toBe(60)
  })

  it('shows an error notification and stays on the form when registration fails', async () => {
    server.use(http.post('/api/auth/register', () => apiError(400, 'DisposableEmailNotAllowed')))
    const { result } = await mountComposable(() => useRegistration(), { attach: true })
    fillAdult(result)
    result.confirmAdult()

    result.submit()
    await vi.waitFor(() =>
      expect(notificationText()).toContain(t('errors.DisposableEmailNotAllowed')),
    )

    expect(notificationText()).toContain(t('common.error'))
    expect(notificationText()).toContain('trace-123')
    expect(result.step.value).toBe('form')
  })

  it('locks resending for 60 seconds after a registration', async () => {
    useCooldownClock()
    const emails = serveResend()
    const { result } = await registerSuccessfully()
    expect(result.resendCooldown.value).toBe(60)

    vi.advanceTimersByTime(1000)
    expect(result.resendCooldown.value).toBe(59)

    result.resend()
    await flushPromises()
    expect(emails).toHaveLength(0)

    vi.advanceTimersByTime(59_000)
    expect(result.resendCooldown.value).toBe(0)
    expect(vi.getTimerCount()).toBe(0)
  })

  it('resends the verification email to the submitted address and restarts the cooldown', async () => {
    useCooldownClock()
    const emails = serveResend()
    const { result } = await registerSuccessfully()
    vi.advanceTimersByTime(60_000)

    result.resend()
    await vi.waitFor(() => expect(result.resendCooldown.value).toBe(60))

    expect(emails).toEqual(['ada@example.test'])
    await vi.waitFor(() =>
      expect(notificationText()).toContain(t('entities.account.verification.linkResentDetail')),
    )
    expect(notificationText()).toContain(t('entities.account.verification.linkResentSummary'))
  })

  it('ignores resend requests while a resend is already in flight', async () => {
    useCooldownClock()
    let release: () => void = () => undefined
    const emails = serveResend(
      () =>
        new Promise<Response>((resolve) => {
          release = () => resolve(noContent())
        }),
    )
    const { result } = await registerSuccessfully()
    vi.advanceTimersByTime(60_000)

    result.resend()
    await vi.waitFor(() => expect(result.isResending.value).toBe(true))
    await vi.waitFor(() => expect(emails).toHaveLength(1))
    result.resend()
    release()
    await vi.waitFor(() => expect(result.isResending.value).toBe(false))

    expect(emails).toHaveLength(1)
  })

  it('reports a failed resend without restarting the cooldown', async () => {
    useCooldownClock()
    serveResend(() => apiError(400, 'RequestValidationFailed'))
    const { result } = await registerSuccessfully()
    vi.advanceTimersByTime(60_000)

    result.resend()
    await vi.waitFor(() =>
      expect(notificationText()).toContain(t('errors.RequestValidationFailed')),
    )

    expect(result.resendCooldown.value).toBe(0)
  })

  it('does not resend before anything was submitted', async () => {
    const emails = serveResend()
    const { result } = await mountComposable(() => useRegistration())

    result.resend()
    await flushPromises()

    expect(emails).toHaveLength(0)
  })

  it('resets the whole flow back to an empty age gate and stops the cooldown', async () => {
    useCooldownClock()
    const { result } = await registerSuccessfully()
    expect(vi.getTimerCount()).toBe(1)

    result.reset()

    expect(result.step.value).toBe('age-gate')
    expect(result.form.firstName).toBe('')
    expect(result.form.minors).toEqual([])
    expect(result.submittedEmail.value).toBe('')
    expect(result.submittedMinorCount.value).toBe(0)
    expect(result.resendCooldown.value).toBe(0)
    expect(result.isSubmitting.value).toBe(false)
    expect(vi.getTimerCount()).toBe(0)
  })

  it('stops the cooldown timer when the component unmounts', async () => {
    useCooldownClock()
    const { wrapper } = await registerSuccessfully()
    expect(vi.getTimerCount()).toBe(1)

    wrapper.unmount()

    expect(vi.getTimerCount()).toBe(0)
  })
})
