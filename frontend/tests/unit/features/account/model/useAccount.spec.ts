import { describe, expect, it, vi } from 'vitest'

import { accountQueryKeys } from '@/entities/account'
import { activityQueryKeys } from '@/entities/activity'
import { useSession } from '@/entities/session'
import { useAccount } from '@/features/account/model/useAccount'

import { buildChildResponse } from '../../../../support/fixtures/account/account'
import { buildUserResponse } from '../../../../support/fixtures/user'
import { t } from '../../../../support/render'
import { apiError, http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../../../support/server'

import { withSetup } from './with-setup'

function serveProfile(user = buildUserResponse()) {
  server.use(http.get('/api/auth/me', () => HttpResponse.json(user)))
}

function serveChildren(children = [buildChildResponse()]) {
  const queries: URLSearchParams[] = []
  server.use(
    http.get('/api/users', ({ request }) => {
      queries.push(new URL(request.url).searchParams)
      return HttpResponse.json({ items: children, total: children.length })
    }),
  )
  return queries
}

describe('useAccount', () => {
  it('loads the profile and the minors of the signed-in user', async () => {
    serveProfile(buildUserResponse({ status: { id: 's', name: 'Activo' }, gender: 'Female' }))
    const queries = serveChildren()

    const { result } = await withSetup(() => useAccount(), { user: {} })
    await vi.waitFor(() => expect(result.children.data.value).toHaveLength(1))
    await vi.waitFor(() => expect(result.profile.data.value).toBeTruthy())

    expect(result.profile.data.value).toEqual({
      id: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phone: '600000000',
      birthDate: '1990-05-10',
      gender: 'Female',
      statusName: 'Activo',
      isAdmin: false,
    })
    expect(result.children.data.value).toEqual([
      {
        id: 'child-1',
        firstName: 'Byron',
        lastName: 'Lovelace',
        birthDate: '2015-03-02',
        gender: 'Male',
      },
    ])
    expect(Object.fromEntries(queries[0] ?? [])).toEqual({
      parentId: 'user-1',
      pageSize: '100',
      sort: 'firstName',
    })
  })

  it('resolves an empty profile for an expired session and skips the minors', async () => {
    const queries = serveChildren()

    const { result } = await withSetup(() => useAccount())
    await vi.waitFor(() => expect(result.profile.isSuccess.value).toBe(true))

    expect(result.profile.data.value).toBeNull()
    expect(queries).toHaveLength(0)

    await result.children.refetch()
    expect(result.children.data.value).toEqual([])
    expect(queries).toHaveLength(0)
  })

  it('updates the profile, caches the response and refreshes the session user', async () => {
    let meRequests = 0
    let received: { body: unknown; csrf: string | null } | undefined
    server.use(
      http.get('/api/auth/me', () => {
        meRequests += 1
        return HttpResponse.json(buildUserResponse(meRequests > 1 ? { firstName: 'Augusta' } : {}))
      }),
      http.put('/api/users/:userId', async ({ request, params }) => {
        expect(params.userId).toBe('user-1')
        received = { body: await request.json(), csrf: request.headers.get('X-CSRF-TOKEN') }
        return HttpResponse.json(buildUserResponse({ firstName: 'Augusta' }))
      }),
    )
    serveChildren([])

    const { result, queryClient } = await withSetup(() => useAccount(), { user: {} })
    const input = {
      firstName: 'Augusta',
      lastName: 'Lovelace',
      email: 'augusta@example.test',
      phone: '611111111',
      birthDate: '1990-05-10',
      gender: 'Female' as const,
    }

    await result.updateProfile.mutateAsync(input)

    expect(received).toEqual({ body: { ...input, parentId: null }, csrf: TEST_CSRF_TOKEN })
    expect(queryClient.getQueryData(accountQueryKeys.me())).toMatchObject({ firstName: 'Augusta' })
    await vi.waitFor(() => expect(useSession().displayName).toBe('Augusta'))
  })

  it('rejects account mutations when nobody is signed in', async () => {
    const { result } = await withSetup(() => useAccount())

    await expect(
      result.changePassword.mutateAsync({ currentPassword: 'a', newPassword: 'b' }),
    ).rejects.toThrow(t('features.account.notAuthenticated'))
    await expect(result.deleteOwnAccount.mutateAsync()).rejects.toThrow(
      t('features.account.notAuthenticated'),
    )
  })

  it('changes the password of the signed-in user', async () => {
    let received: unknown
    serveProfile()
    serveChildren([])
    server.use(
      http.patch('/api/users/:userId/password', async ({ request, params }) => {
        expect(params.userId).toBe('user-1')
        received = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { result } = await withSetup(() => useAccount(), { user: {} })
    await result.changePassword.mutateAsync({
      currentPassword: 'old-password',
      newPassword: 'new-password-123',
    })

    expect(received).toEqual({ currentPassword: 'old-password', newPassword: 'new-password-123' })
  })

  it('adds, updates and deletes minors and refreshes the household data', async () => {
    const requests: { method: string; path: string; body: unknown }[] = []
    serveProfile()
    const queries = serveChildren()
    server.use(
      http.post('/api/users/:userId/children', async ({ request }) => {
        requests.push({
          method: 'POST',
          path: new URL(request.url).pathname,
          body: await request.json(),
        })
        return HttpResponse.json(buildChildResponse({ id: 'child-2' }), { status: 201 })
      }),
      http.put('/api/users/:userId', async ({ request }) => {
        requests.push({
          method: 'PUT',
          path: new URL(request.url).pathname,
          body: await request.json(),
        })
        return HttpResponse.json(buildChildResponse())
      }),
      http.delete('/api/users/:userId', ({ request }) => {
        requests.push({ method: 'DELETE', path: new URL(request.url).pathname, body: null })
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { result, queryClient } = await withSetup(() => useAccount(), { user: {} })
    await vi.waitFor(() => expect(queries).toHaveLength(1))
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
    const minor = {
      firstName: 'Byron',
      lastName: 'Lovelace',
      birthDate: '2015-03-02',
      gender: 'Male' as const,
    }

    await expect(result.addChild.mutateAsync(minor)).resolves.toMatchObject({ id: 'child-2' })
    await result.updateChild.mutateAsync({ childId: 'child-1', input: minor })
    await result.deleteChild.mutateAsync('child-1')

    expect(requests).toEqual([
      { method: 'POST', path: '/api/users/user-1/children', body: minor },
      { method: 'PUT', path: '/api/users/child-1', body: { ...minor, parentId: 'user-1' } },
      { method: 'DELETE', path: '/api/users/child-1', body: null },
    ])
    expect(invalidate).toHaveBeenCalledWith({ queryKey: accountQueryKeys.children() })
    expect(invalidate).toHaveBeenCalledWith({ queryKey: activityQueryKeys.householdMembers() })
    expect(invalidate).toHaveBeenCalledTimes(6)
    await vi.waitFor(() => expect(queries.length).toBeGreaterThan(1))
  })

  it('deletes the account, signs out, clears cached data and goes home', async () => {
    const calls: string[] = []
    serveProfile()
    serveChildren([])
    server.use(
      http.delete('/api/users/:userId', ({ params }) => {
        calls.push(`delete:${String(params.userId)}`)
        return new HttpResponse(null, { status: 204 })
      }),
      http.post('/api/auth/logout', () => {
        calls.push('logout')
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { result, router, queryClient } = await withSetup(() => useAccount(), {
      user: {},
      route: '/account',
    })
    await vi.waitFor(() => expect(result.profile.isSuccess.value).toBe(true))
    queryClient.setQueryData(['cached', 'elsewhere'], 'stale')

    await result.deleteOwnAccount.mutateAsync()

    expect(calls).toEqual(['delete:user-1', 'logout'])
    expect(useSession().user).toBeNull()
    expect(queryClient.getQueryData(['cached', 'elsewhere'])).toBeUndefined()
    expect(router.currentRoute.value.name).toBe('home')
  })

  it('still clears the session and leaves when signing out fails after deletion', async () => {
    serveProfile()
    serveChildren([])
    server.use(
      http.delete('/api/users/:userId', () => new HttpResponse(null, { status: 204 })),
      http.post('/api/auth/logout', () => apiError(500)),
    )

    const { result, router } = await withSetup(() => useAccount(), {
      user: {},
      route: '/account',
    })

    await expect(result.deleteOwnAccount.mutateAsync()).rejects.toMatchObject({ status: 500 })

    expect(useSession().user).toBeNull()
    expect(router.currentRoute.value.name).toBe('home')
  })
})
