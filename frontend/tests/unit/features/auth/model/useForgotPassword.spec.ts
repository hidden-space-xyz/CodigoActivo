import { describe, expect, it, vi } from 'vitest'

import { useForgotPassword } from '@/features/auth'

import { mountComposable } from '../../../../support/fixtures/auth-register/composable'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

describe('useForgotPassword', () => {
  it('submits the trimmed email and marks the link as sent', async () => {
    const bodies: unknown[] = []
    server.use(
      http.post('/api/auth/forgot-password', async ({ request }) => {
        bodies.push(await request.json())
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { result } = await mountComposable(() => useForgotPassword())
    result.form.email = '  ada@example.test  '

    result.submit()
    await vi.waitFor(() => expect(result.sent.value).toBe(true))

    expect(bodies).toEqual([{ email: 'ada@example.test' }])
    expect(result.isError.value).toBe(false)
  })

  it('flags an error and does not mark the link as sent when the API fails', async () => {
    server.use(http.post('/api/auth/forgot-password', () => apiError(500)))
    const { result } = await mountComposable(() => useForgotPassword())
    result.form.email = 'ada@example.test'

    result.submit()
    await vi.waitFor(() => expect(result.isError.value).toBe(true))

    expect(result.sent.value).toBe(false)
  })
})
