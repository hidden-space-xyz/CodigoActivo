import { describe, expect, it } from 'vitest'

import {
  changeUserTypeRequest,
  deleteUserRequest,
  getUserListPageRequest,
  getUserRequest,
  getUserStatusesRequest,
  getUserTypesRequest,
  resetUserTwoFactorRequest,
  setUserAdminRequest,
  updateUserRequest,
} from '@/entities/user/api/requests'

import { apiError, http, HttpResponse, noContent, paged, server } from '../../../../support/server'
import { buildUserResponse, userStatusTypes, userTypes } from '../../../../support/builders'
import { queryOf } from '../../../../support/dom'

describe('user requests', () => {
  it('pages the admin user list and maps each user', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/users', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildUserResponse({ id: 'u2' })], 40))
      }),
    )

    const page = await getUserListPageRequest({ page: 2, pageSize: 20, name: 'ada' })

    expect(page.total).toBe(40)
    expect(page.items).toEqual([expect.objectContaining({ id: 'u2', firstName: 'Ada' })])
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '2', pageSize: '20', name: 'ada' })
  })

  it('loads a user, throwing on 404', async () => {
    server.use(
      http.get('/api/users/u1', () => HttpResponse.json(buildUserResponse({ id: 'u1' }))),
      http.get('/api/users/missing', () => apiError(404, 'UserNotFound')),
    )

    await expect(getUserRequest('u1')).resolves.toMatchObject({ id: 'u1', lastName: 'Lovelace' })
    await expect(getUserRequest('missing')).rejects.toMatchObject({
      status: 404,
      code: 'UserNotFound',
    })
  })

  it('lists the user types and statuses', async () => {
    server.use(
      http.get('/api/users/types', () => HttpResponse.json(userTypes)),
      http.get('/api/users/status-types', () => HttpResponse.json(userStatusTypes)),
    )

    await expect(getUserTypesRequest()).resolves.toEqual([
      { id: 'type-participant', name: 'Participant', color: '#00AA00' },
      { id: 'type-member', name: 'Member', color: '#0000AA' },
    ])
    await expect(getUserStatusesRequest()).resolves.toEqual([
      { id: 'status-active', name: 'Active', color: '#00FF00' },
      { id: 'status-blocked', name: 'Blocked', color: '#FF0000' },
    ])
  })

  it('updates, retypes and deletes users', async () => {
    const calls: { method: string; path: string; query: Record<string, string>; body: unknown }[] =
      []
    const record = async ({ request }: { request: Request }) => {
      const text = await request.text()
      const url = new URL(request.url)
      calls.push({
        method: request.method,
        path: url.pathname,
        query: queryOf(request.url),
        body: text ? (JSON.parse(text) as unknown) : null,
      })
      return request.method === 'DELETE' ? noContent() : HttpResponse.json(buildUserResponse())
    }
    server.use(
      http.put('/api/users/u1', record),
      http.patch('/api/users/u1/change-type', record),
      http.delete('/api/users/u1', record),
    )
    const input = {
      firstName: 'Grace',
      lastName: 'Hopper',
      email: 'grace@example.test',
      phone: '611111111',
      secondaryPhone: '622222222',
      birthDate: null,
      nationalId: 'X1234567L',
      promotionalConsent: true,
      gender: 'Female',
      parentId: null,
      currentPassword: null,
    } as const

    await updateUserRequest('u1', input)
    await changeUserTypeRequest('u1', 'type-member')
    await deleteUserRequest('u1')

    expect(calls).toEqual([
      { method: 'PUT', path: '/api/users/u1', query: {}, body: input },
      {
        method: 'PATCH',
        path: '/api/users/u1/change-type',
        query: { userTypeId: 'type-member' },
        body: null,
      },
      { method: 'DELETE', path: '/api/users/u1', query: {}, body: null },
    ])
  })

  it('grants and revokes the admin role and resets the second factor', async () => {
    const bodies: unknown[] = []
    const record = async ({ request }: { request: Request }) => {
      bodies.push(await request.json())
      return noContent()
    }
    server.use(
      http.patch('/api/users/u1/admin', record),
      http.post('/api/users/u1/two-factor/reset', record),
    )

    await setUserAdminRequest('u1', true, 'Str0ngPass!23')
    await setUserAdminRequest('u1', false, null)
    await resetUserTwoFactorRequest('u1', 'Str0ngPass!23')

    expect(bodies).toEqual([
      { isAdmin: true, currentPassword: 'Str0ngPass!23' },
      { isAdmin: false, currentPassword: null },
      { currentPassword: 'Str0ngPass!23' },
    ])
  })
})
