import { describe, expect, it } from 'vitest'

import { useSession } from '@/entities/session'

import { buildAuthUser, buildUserResponse } from '../../../../support/fixtures/user'
import { http, HttpResponse, server } from '../../../../support/server'

describe('useSession', () => {
  it('returns the same singleton everywhere', () => {
    expect(useSession()).toBe(useSession())
  })

  it('starts anonymous and derives state from the user set locally', () => {
    const session = useSession()

    expect(session.user).toBeNull()
    expect(session.isAuthenticated).toBe(false)
    expect(session.displayName).toBe('')
    expect(session.isAdmin).toBe(false)

    session.setUser(buildAuthUser({ firstName: 'Grace', isAdmin: true }))

    expect(session.isAuthenticated).toBe(true)
    expect(session.displayName).toBe('Grace')
    expect(session.isAdmin).toBe(true)

    session.clear()

    expect(session.user).toBeNull()
    expect(session.isAuthenticated).toBe(false)
  })

  it('returns the cached user without asking the API', async () => {
    let calls = 0
    server.use(
      http.get('/api/auth/me', () => {
        calls += 1
        return HttpResponse.json(buildUserResponse())
      }),
    )
    const user = buildAuthUser({ id: 'cached' })
    useSession().setUser(user)

    await expect(useSession().resolve()).resolves.toStrictEqual(user)
    expect(calls).toBe(0)
  })

  it('loads the user once for concurrent callers and caches it', async () => {
    let calls = 0
    server.use(
      http.get('/api/auth/me', () => {
        calls += 1
        return HttpResponse.json(buildUserResponse({ firstName: 'Grace' }))
      }),
    )
    const session = useSession()

    const [first, second] = await Promise.all([session.resolve(), session.resolve()])
    const third = await session.resolve()

    expect(first?.firstName).toBe('Grace')
    expect(second).toStrictEqual(first)
    expect(third).toStrictEqual(first)
    expect(session.isAuthenticated).toBe(true)
    expect(calls).toBe(1)
  })

  it('does not cache an anonymous result, so later calls ask again', async () => {
    let calls = 0
    server.use(
      http.get('/api/auth/me', () => {
        calls += 1
        return new HttpResponse(null, { status: 401 })
      }),
    )
    const session = useSession()

    await expect(session.resolve()).resolves.toBeNull()
    await expect(session.resolve()).resolves.toBeNull()

    expect(session.isAuthenticated).toBe(false)
    expect(calls).toBe(2)
  })

  it('lets a later call retry after the in-flight request fails', async () => {
    let fail = true
    server.use(
      http.get('/api/auth/me', () =>
        fail
          ? HttpResponse.json({ title: 'boom' }, { status: 500 })
          : HttpResponse.json(buildUserResponse()),
      ),
    )
    const session = useSession()

    await expect(session.resolve()).rejects.toMatchObject({ status: 500 })
    fail = false

    await expect(session.resolve()).resolves.toMatchObject({ id: 'user-1' })
  })
})
