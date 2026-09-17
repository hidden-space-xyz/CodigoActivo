import { describe, expect, it } from 'vitest'

import {
  changeUserTypeRequest,
  deleteUserRequest,
  getUserRequest,
  getUsersPageRequest,
  resetUserTwoFactorRequest,
  setUserAdminRequest,
  updateUserRequest,
} from '@/entities/user'

import { buildUserResponse } from '../../../../support/fixtures/user'
import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'

const noContent = () => new HttpResponse(null, { status: 204 })

describe('user requests', () => {
  it('pages the admin users table and maps each user', async () => {
    let query: Record<string, string> = {}
    server.use(
      http.get('/api/users', ({ request }) => {
        query = Object.fromEntries(new URL(request.url).searchParams)
        return HttpResponse.json(paged([buildUserResponse({ id: 'u2' })], 40))
      }),
    )

    const page = await getUsersPageRequest({ page: 2, pageSize: 20, name: 'ada' })

    expect(page.total).toBe(40)
    expect(page.items).toEqual([expect.objectContaining({ id: 'u2', firstName: 'Ada' })])
    expect(query).toEqual({ page: '2', pageSize: '20', name: 'ada' })
  })

  describe('getUserRequest', () => {
    it('maps the user', async () => {
      server.use(
        http.get('/api/users/u1', () => HttpResponse.json(buildUserResponse({ id: 'u1' }))),
      )

      await expect(getUserRequest('u1')).resolves.toMatchObject({ id: 'u1', lastName: 'Lovelace' })
    })

    it('resolves null when the API sends no body', async () => {
      server.use(http.get('/api/users/u1', noContent))

      await expect(getUserRequest('u1')).resolves.toBeNull()
    })

    it('throws instead of resolving null on 404', async () => {
      server.use(http.get('/api/users/missing', () => apiError(404, 'UserNotFound')))

      await expect(getUserRequest('missing')).rejects.toMatchObject({
        status: 404,
        code: 'UserNotFound',
      })
    })
  })

  it('updates a user and maps the response, or null without a body', async () => {
    const bodies: unknown[] = []
    let empty = false
    server.use(
      http.put('/api/users/u1', async ({ request }) => {
        bodies.push(await request.json())
        return empty ? noContent() : HttpResponse.json(buildUserResponse({ firstName: 'Grace' }))
      }),
    )
    const input = {
      firstName: 'Grace',
      lastName: 'Hopper',
      email: 'grace@example.test',
      phone: '611111111',
      birthDate: '1980-12-09',
      gender: 'Female',
      parentId: null,
    } as const

    await expect(updateUserRequest('u1', input)).resolves.toMatchObject({ firstName: 'Grace' })
    empty = true
    await expect(updateUserRequest('u1', input)).resolves.toBeNull()
    expect(bodies).toEqual([input, input])
  })

  it('deletes a user returning the raw response', async () => {
    server.use(http.delete('/api/users/u1', noContent))

    await expect(deleteUserRequest('u1')).resolves.toMatchObject({ status: 204 })
  })

  it('changes the user type through the query string', async () => {
    let query: Record<string, string> = {}
    let empty = false
    server.use(
      http.patch('/api/users/u1/change-type', ({ request }) => {
        query = Object.fromEntries(new URL(request.url).searchParams)
        return empty
          ? noContent()
          : HttpResponse.json(buildUserResponse({ type: { id: 'type-member', name: 'Socio' } }))
      }),
    )

    await expect(changeUserTypeRequest('u1', 'type-member')).resolves.toMatchObject({
      type: { id: 'type-member', name: 'Socio', color: null },
    })
    empty = true
    await expect(changeUserTypeRequest('u1', 'type-member')).resolves.toBeNull()
    expect(query).toEqual({ userTypeId: 'type-member' })
  })

  it('grants and revokes the admin role', async () => {
    const bodies: unknown[] = []
    server.use(
      http.patch('/api/users/u1/admin', async ({ request }) => {
        bodies.push(await request.json())
        return noContent()
      }),
    )

    await setUserAdminRequest('u1', true, 'Str0ngPass!23')
    await setUserAdminRequest('u1', false)

    expect(bodies).toEqual([
      { isAdmin: true, currentPassword: 'Str0ngPass!23' },
      { isAdmin: false, currentPassword: null },
    ])
  })

  it('resets the second factor of a user with the admin password', async () => {
    let body: unknown
    server.use(
      http.post('/api/users/u1/two-factor/reset', async ({ request }) => {
        body = await request.json()
        return noContent()
      }),
    )

    await resetUserTwoFactorRequest('u1', 'Str0ngPass!23')

    expect(body).toEqual({ currentPassword: 'Str0ngPass!23' })
  })
})
