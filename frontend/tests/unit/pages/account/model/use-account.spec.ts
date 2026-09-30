import { describe, expect, it, vi } from 'vitest'

import { accountKeys } from '@/entities/account'
import { activityKeys } from '@/entities/activity'
import { useAccount } from '@/pages/account/model/use-account'

import { http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../../../support/server'
import { buildDependentResponse, buildUserResponse } from '../../../../support/builders'
import { mountComposable } from '../../../../support/render'
import { sessionOf } from '../../../../support/session'

function serveProfile(user = buildUserResponse()) {
  server.use(http.get('/api/auth/me', () => HttpResponse.json(user)))
}

function serveChildren(children = [buildDependentResponse()]) {
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
    serveProfile(
      buildUserResponse({
        status: { id: 's', name: 'Activo', color: '#00FF00' },
        gender: 'Female',
      }),
    )
    const queries = serveChildren()

    const { result } = await mountComposable(() => useAccount(), { user: {} })
    await vi.waitFor(() => expect(result.children.data.value).toHaveLength(1))
    await vi.waitFor(() => expect(result.profile.data.value).toBeTruthy())

    expect(result.profile.data.value).toEqual({
      id: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phone: '600000000',
      secondaryPhone: '',
      nationalId: '12345678Z',
      promotionalConsent: false,
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

    const { result } = await mountComposable(() => useAccount())
    await vi.waitFor(() => expect(result.profile.isSuccess.value).toBe(true))

    expect(result.profile.data.value).toBeNull()
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

    const { result, queryClient } = await mountComposable(() => useAccount(), { user: {} })
    const input = {
      firstName: 'Augusta',
      lastName: 'Lovelace',
      email: 'augusta@example.test',
      phone: '611111111',
      secondaryPhone: '622222222',
      nationalId: 'X1234567L',
      promotionalConsent: true,
      gender: 'Female' as const,
      currentPassword: 'Str0ngPass!23',
    }

    await result.updateProfile.mutateAsync(input)

    expect(received).toEqual({
      body: { ...input, birthDate: null, parentId: null },
      csrf: TEST_CSRF_TOKEN,
    })
    expect(queryClient.getQueryData(accountKeys.profile())).toMatchObject({ firstName: 'Augusta' })
    await vi.waitFor(() => expect(sessionOf(queryClient).displayName).toBe('Augusta'))
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

    const { result } = await mountComposable(() => useAccount(), { user: {} })
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
        return HttpResponse.json(buildDependentResponse({ id: 'child-2' }), { status: 201 })
      }),
      http.put('/api/users/:userId', async ({ request }) => {
        requests.push({
          method: 'PUT',
          path: new URL(request.url).pathname,
          body: await request.json(),
        })
        return HttpResponse.json(buildDependentResponse())
      }),
      http.delete('/api/users/:userId', ({ request }) => {
        requests.push({ method: 'DELETE', path: new URL(request.url).pathname, body: null })
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { result, queryClient } = await mountComposable(() => useAccount(), { user: {} })
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
      {
        method: 'PUT',
        path: '/api/users/child-1',
        body: { ...minor, promotionalConsent: false, parentId: 'user-1' },
      },
      { method: 'DELETE', path: '/api/users/child-1', body: null },
    ])
    expect(invalidate).toHaveBeenCalledWith({ queryKey: accountKeys.children() })
    expect(invalidate).toHaveBeenCalledWith({ queryKey: activityKeys.householdMembers() })
    expect(invalidate).toHaveBeenCalledTimes(6)
    await vi.waitFor(() => expect(queries.length).toBeGreaterThan(1))
  })

  it('does not expose a self-deletion mutation any more', async () => {
    serveProfile()
    serveChildren([])

    const { result } = await mountComposable(() => useAccount(), { user: {} })

    expect(Object.keys(result)).toEqual([
      'profile',
      'children',
      'updateProfile',
      'changePassword',
      'addChild',
      'updateChild',
      'deleteChild',
    ])
  })
})
