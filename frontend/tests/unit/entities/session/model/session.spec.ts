import { describe, expect, it } from 'vitest'

import {
  currentUser,
  endSession,
  refreshSession,
  resolveSession,
  startSession,
} from '@/entities/session'

import { createTestQueryClient } from '../../../../support/render'
import { http, HttpResponse, server } from '../../../../support/server'
import { buildUserResponse } from '../../../../support/builders'
import { buildAuthUser } from '../../../../support/models'

function countMeRequests(response: () => Response): { count: number } {
  const calls = { count: 0 }
  server.use(
    http.get('/api/auth/me', () => {
      calls.count += 1
      return response()
    }),
  )
  return calls
}

describe('session', () => {
  it('knows no user until one is resolved or started', () => {
    const queryClient = createTestQueryClient()

    expect(currentUser(queryClient)).toBeNull()

    startSession(queryClient, buildAuthUser({ firstName: 'Grace' }))

    expect(currentUser(queryClient)?.firstName).toBe('Grace')
  })

  it('returns a known user without asking the API', async () => {
    const calls = countMeRequests(() => HttpResponse.json(buildUserResponse()))
    const queryClient = createTestQueryClient()
    const user = buildAuthUser({ id: 'cached' })
    startSession(queryClient, user)

    await expect(resolveSession(queryClient)).resolves.toStrictEqual(user)
    expect(calls.count).toBe(0)
  })

  it('loads the user once for concurrent callers and keeps it', async () => {
    const calls = countMeRequests(() =>
      HttpResponse.json(buildUserResponse({ firstName: 'Grace' })),
    )
    const queryClient = createTestQueryClient()

    const [first, second] = await Promise.all([
      resolveSession(queryClient),
      resolveSession(queryClient),
    ])
    const third = await resolveSession(queryClient)

    expect(first?.firstName).toBe('Grace')
    expect(second).toStrictEqual(first)
    expect(third).toStrictEqual(first)
    expect(calls.count).toBe(1)
  })

  it('asks the API again for a guest', async () => {
    const calls = countMeRequests(() => new HttpResponse(null, { status: 401 }))
    const queryClient = createTestQueryClient()

    await expect(resolveSession(queryClient)).resolves.toBeNull()
    await expect(resolveSession(queryClient)).resolves.toBeNull()

    expect(calls.count).toBe(2)
  })

  it('lets a later call retry after the lookup fails', async () => {
    let fail = true
    server.use(
      http.get('/api/auth/me', () =>
        fail
          ? HttpResponse.json({ title: 'boom' }, { status: 500 })
          : HttpResponse.json(buildUserResponse()),
      ),
    )
    const queryClient = createTestQueryClient()

    await expect(resolveSession(queryClient)).rejects.toMatchObject({ status: 500 })
    fail = false

    await expect(resolveSession(queryClient)).resolves.toMatchObject({ id: 'user-1' })
  })

  it('reloads a known user when refreshed', async () => {
    countMeRequests(() => HttpResponse.json(buildUserResponse({ firstName: 'Augusta' })))
    const queryClient = createTestQueryClient()
    startSession(queryClient, buildAuthUser({ firstName: 'Ada' }))

    await expect(refreshSession(queryClient)).resolves.toMatchObject({ firstName: 'Augusta' })
    expect(currentUser(queryClient)?.firstName).toBe('Augusta')
  })

  it('forgets the user and every other cached query and mutation when it ends', () => {
    const queryClient = createTestQueryClient()
    startSession(queryClient, buildAuthUser())
    queryClient.setQueryData(['account', 'me'], { id: 'user-1' })
    queryClient.getMutationCache().build(queryClient, { mutationFn: () => Promise.resolve() })

    endSession(queryClient)

    expect(currentUser(queryClient)).toBeNull()
    expect(queryClient.getQueryData(['account', 'me'])).toBeUndefined()
    expect(queryClient.getMutationCache().getAll()).toHaveLength(0)
  })
})
