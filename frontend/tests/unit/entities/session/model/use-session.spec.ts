import { describe, expect, it } from 'vitest'

import { endSession, startSession, useSession } from '@/entities/session'

import { mountComposable } from '../../../../support/render'
import { http, HttpResponse, server } from '../../../../support/server'
import { buildUserResponse } from '../../../../support/builders'
import { buildAuthUser } from '../../../../support/models'

describe('useSession', () => {
  it('shows a guest when the API knows no user', async () => {
    const { result } = await mountComposable(() => useSession())

    expect(result.user).toBeNull()
    expect(result.isAuthenticated).toBe(false)
    expect(result.displayName).toBe('')
    expect(result.isAdmin).toBe(false)
  })

  it('loads the user once when nothing resolved the session yet', async () => {
    let calls = 0
    server.use(
      http.get('/api/auth/me', () => {
        calls += 1
        return HttpResponse.json(buildUserResponse({ firstName: 'Grace', isAdmin: true }))
      }),
    )

    const { result } = await mountComposable(() => useSession())

    expect(result.displayName).toBe('Grace')
    expect(result.isAdmin).toBe(true)
    expect(calls).toBe(1)
  })

  it('follows the session as it starts and ends', async () => {
    const { result, queryClient } = await mountComposable(() => useSession())

    startSession(queryClient, buildAuthUser({ firstName: 'Ada' }))
    expect(result.isAuthenticated).toBe(true)
    expect(result.displayName).toBe('Ada')

    endSession(queryClient)
    expect(result.user).toBeNull()
  })
})
