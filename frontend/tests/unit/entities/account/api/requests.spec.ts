import { describe, expect, it } from 'vitest'

import {
  addAccountChildRequest,
  changeAccountPasswordRequest,
  deleteAccountChildRequest,
  deleteAccountRequest,
  getAccountCertificatesRequest,
  getAccountChildrenRequest,
  getAccountHistoryRequest,
  getAccountProfileRequest,
  saveAccountEventRatingRequest,
  updateAccountChildRequest,
  updateAccountProfileRequest,
} from '@/entities/account'
import { ApiError } from '@/shared/api'
import type { EventCertificateResponse, EventHistoryResponse } from '@/shared/api/generated/models'

import { buildUserResponse } from '../../../../support/fixtures/user'
import {
  apiError,
  http,
  HttpResponse,
  paged,
  server,
  TEST_CSRF_TOKEN,
} from '../../../../support/server'

describe('account requests', () => {
  describe('getAccountProfileRequest', () => {
    it('maps the signed-in user', async () => {
      server.use(http.get('/api/auth/me', () => HttpResponse.json(buildUserResponse())))

      await expect(getAccountProfileRequest()).resolves.toMatchObject({
        id: 'user-1',
        firstName: 'Ada',
        statusName: 'Active',
      })
    })

    it.each([401, 403])('resolves null when the API answers %i', async (status) => {
      server.use(http.get('/api/auth/me', () => apiError(status)))

      await expect(getAccountProfileRequest()).resolves.toBeNull()
    })

    it('rethrows other failures', async () => {
      server.use(http.get('/api/auth/me', () => apiError(500)))

      await expect(getAccountProfileRequest()).rejects.toBeInstanceOf(ApiError)
    })
  })

  it('lists the children of a parent sorted by first name', async () => {
    let url: URL | undefined
    server.use(
      http.get('/api/users', ({ request }) => {
        url = new URL(request.url)
        return HttpResponse.json(paged([buildUserResponse({ id: 'child-1', firstName: 'Byron' })]))
      }),
    )

    const children = await getAccountChildrenRequest('parent-1')

    expect(children).toEqual([
      {
        id: 'child-1',
        firstName: 'Byron',
        lastName: 'Lovelace',
        birthDate: '1990-05-10',
        gender: 'Female',
      },
    ])
    expect(Object.fromEntries(url?.searchParams ?? [])).toEqual({
      parentId: 'parent-1',
      pageSize: '100',
      sort: 'firstName',
    })
  })

  it('updates the own profile with a null parent and returns the server profile', async () => {
    let body: unknown
    let csrf: string | null = null
    server.use(
      http.put('/api/users/:userId', async ({ request, params }) => {
        body = await request.json()
        csrf = request.headers.get('X-CSRF-TOKEN')
        return HttpResponse.json(buildUserResponse({ id: String(params.userId), lastName: 'King' }))
      }),
    )

    const profile = await updateAccountProfileRequest('user-1', {
      firstName: 'Ada',
      lastName: 'King',
      email: 'ada@example.test',
      phone: '600000000',
      birthDate: '1990-05-10',
      gender: 'Female',
    })

    expect(profile.lastName).toBe('King')
    expect(body).toEqual({
      firstName: 'Ada',
      lastName: 'King',
      email: 'ada@example.test',
      phone: '600000000',
      birthDate: '1990-05-10',
      gender: 'Female',
      parentId: null,
    })
    expect(csrf).toBe(TEST_CSRF_TOKEN)
  })

  it('deletes the account and a child through the user endpoint', async () => {
    const deleted: string[] = []
    server.use(
      http.delete('/api/users/:userId', ({ params }) => {
        deleted.push(String(params.userId))
        return new HttpResponse(null, { status: 204 })
      }),
    )

    await expect(deleteAccountRequest('user-1')).resolves.toBeUndefined()
    await expect(deleteAccountChildRequest('child-1')).resolves.toBeUndefined()
    expect(deleted).toEqual(['user-1', 'child-1'])
  })

  it('sends the current and new password', async () => {
    let body: unknown
    server.use(
      http.patch('/api/users/user-1/password', async ({ request }) => {
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )

    await changeAccountPasswordRequest('user-1', { currentPassword: 'old', newPassword: 'new' })

    expect(body).toEqual({ currentPassword: 'old', newPassword: 'new' })
  })

  it('surfaces a rejected password change as an ApiError with its code', async () => {
    server.use(
      http.patch('/api/users/user-1/password', () => apiError(400, 'UserCurrentPasswordIncorrect')),
    )

    await expect(
      changeAccountPasswordRequest('user-1', { currentPassword: 'bad', newPassword: 'new' }),
    ).rejects.toMatchObject({ status: 400, code: 'UserCurrentPasswordIncorrect' })
  })

  it('registers a minor and maps the created child', async () => {
    let body: unknown
    server.use(
      http.post('/api/users/parent-1/children', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json(buildUserResponse({ id: 'child-2', firstName: 'Byron' }))
      }),
    )

    const child = await addAccountChildRequest('parent-1', {
      firstName: 'Byron',
      lastName: 'King',
      birthDate: '2015-01-02',
      gender: 'Male',
    })

    expect(child).toMatchObject({ id: 'child-2', firstName: 'Byron' })
    expect(body).toEqual({
      firstName: 'Byron',
      lastName: 'King',
      birthDate: '2015-01-02',
      gender: 'Male',
    })
  })

  it('updates a minor keeping the parent link', async () => {
    let body: unknown
    server.use(
      http.put('/api/users/child-1', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json(buildUserResponse({ id: 'child-1', firstName: 'Byron' }))
      }),
    )

    const child = await updateAccountChildRequest('child-1', 'parent-1', {
      firstName: 'Byron',
      lastName: 'King',
      birthDate: '2015-01-02',
      gender: 'Male',
    })

    expect(child.id).toBe('child-1')
    expect(body).toMatchObject({ parentId: 'parent-1', firstName: 'Byron' })
  })

  it('maps the event history', async () => {
    const history: EventHistoryResponse[] = [{ eventId: 'event-1', title: 'Día', activities: [] }]
    server.use(http.get('/api/me/event-history', () => HttpResponse.json(history)))

    const entries = await getAccountHistoryRequest()

    expect(entries).toHaveLength(1)
    expect(entries[0]).toMatchObject({ eventId: 'event-1', title: 'Día', rating: null })
  })

  it('returns an empty history when the API sends no body', async () => {
    server.use(http.get('/api/me/event-history', () => new HttpResponse(null, { status: 204 })))

    await expect(getAccountHistoryRequest()).resolves.toEqual([])
  })

  it('maps the certificates and treats an empty body as no certificates', async () => {
    const certificates: EventCertificateResponse[] = [{ code: 'CA-1', userId: 'user-1' }]
    server.use(http.get('/api/me/certificates', () => HttpResponse.json(certificates)))

    await expect(getAccountCertificatesRequest()).resolves.toEqual([
      expect.objectContaining({ code: 'CA-1', participantId: 'user-1' }),
    ])

    server.use(http.get('/api/me/certificates', () => new HttpResponse(null, { status: 204 })))
    await expect(getAccountCertificatesRequest()).resolves.toEqual([])
  })

  it('saves an event rating with trimmed comments and maps the stored rating', async () => {
    let body: unknown
    server.use(
      http.put('/api/events/event-1/rating', async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ score: 5, mostLiked: 'Todo', leastLiked: null })
      }),
    )

    const rating = await saveAccountEventRatingRequest('event-1', {
      score: 5,
      mostLiked: ' Todo ',
      leastLiked: '',
      suggestions: ' ',
    })

    expect(body).toEqual({ score: 5, mostLiked: 'Todo', leastLiked: null, suggestions: null })
    expect(rating).toEqual({ score: 5, mostLiked: 'Todo', leastLiked: '', suggestions: '' })
  })
})
