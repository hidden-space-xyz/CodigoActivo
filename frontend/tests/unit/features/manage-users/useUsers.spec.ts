import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useUsers } from '@/features/manage-users'
import type { UserResponse } from '@/shared/api/generated/models'

import { without } from '../../../support/fixtures/admin-content/builders'
import { queryOf, withSetup } from '../../../support/fixtures/admin-content/helpers'
import { buildUserResponse } from '../../../support/fixtures/user'
import { apiError, http, HttpResponse, server } from '../../../support/server'

function serveUserPages(pages: UserResponse[][]) {
  const urls: string[] = []
  const total = pages.reduce((sum, page) => sum + page.length, 0)
  server.use(
    http.get('/api/users', ({ request }) => {
      urls.push(request.url)
      const page = Number(new URL(request.url).searchParams.get('page') ?? '1')
      return HttpResponse.json({ items: pages[page - 1] ?? [], total })
    }),
  )
  return urls
}

describe('useUsers', () => {
  it('maps table rows and merges the relation filter into the request', async () => {
    const urls = serveUserPages([[without(buildUserResponse({ email: null }), 'status')]])
    const { result } = await withSetup(() => useUsers())

    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))
    expect(result.table.items.value[0]).toMatchObject({ id: 'user-1', email: '', status: null })
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'firstName' })

    result.relationFilter.value = { label: 'Dependents of Ada', params: { parentId: 'user-1' } }
    await flushPromises()

    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({ parentId: 'user-1' })
    expect(result.table.filterParams.value).toEqual({ parentId: 'user-1' })
  })

  it('filters the table by promotional consent and DNI/NIE', async () => {
    const urls = serveUserPages([[buildUserResponse()]])
    const { result } = await withSetup(() => useUsers())
    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))

    result.table.columnFilter('promotionalConsent').value = false
    result.table.columnFilter('nationalId').value = 'x123'
    await flushPromises()

    expect(result.table.filterParams.value).toEqual({
      promotionalConsent: false,
      nationalId: 'x123',
    })
    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({
      promotionalConsent: 'false',
      nationalId: 'x123',
    })
  })

  it('fetches every page with the current filters and sort for exports', async () => {
    const firstPage = Array.from({ length: 100 }, (_, index) =>
      buildUserResponse({ id: `user-${index}` }),
    )
    const urls = serveUserPages([firstPage, [buildUserResponse({ id: 'user-last' })]])
    const { result } = await withSetup(() => useUsers())
    result.table.columnFilter('isAdmin').value = true
    result.table.onSortChange({ prop: 'email', order: 'descending' })
    await flushPromises()

    const users = await result.fetchAllUsers()

    expect(users).toHaveLength(101)
    expect(users.at(-1)?.id).toBe('user-last')
    expect(urls.slice(-2).map(queryOf)).toEqual([
      { isAdmin: 'true', sort: '-email', page: '1', pageSize: '100' },
      { isAdmin: 'true', sort: '-email', page: '2', pageSize: '100' },
    ])
  })

  it('loads a single user and propagates missing users as errors', async () => {
    serveUserPages([[]])
    server.use(
      http.get('/api/users/:userId', ({ params }) =>
        params.userId === 'missing'
          ? apiError(404)
          : HttpResponse.json(buildUserResponse({ id: String(params.userId) })),
      ),
    )
    const { result } = await withSetup(() => useUsers())

    await expect(result.fetchOne('user-9')).resolves.toMatchObject({ id: 'user-9' })
    await expect(result.fetchOne('missing')).rejects.toMatchObject({ status: 404 })
  })

  it('invalidates all user queries after each mutation succeeds', async () => {
    serveUserPages([[]])
    const requests: string[] = []
    server.use(
      http.put('/api/users/:userId', async ({ request }) => {
        requests.push(`PUT ${JSON.stringify(await request.json())}`)
        return HttpResponse.json(buildUserResponse())
      }),
      http.delete('/api/users/:userId', ({ params }) => {
        requests.push(`DELETE ${String(params.userId)}`)
        return new HttpResponse(null, { status: 204 })
      }),
      http.patch('/api/users/:userId/change-type', ({ request }) => {
        requests.push(`TYPE ${new URL(request.url).search}`)
        return HttpResponse.json(buildUserResponse())
      }),
      http.patch('/api/users/:userId/admin', async ({ request }) => {
        requests.push(`ADMIN ${JSON.stringify(await request.json())}`)
        return new HttpResponse(null, { status: 204 })
      }),
      http.post('/api/users/:userId/two-factor/reset', async ({ request, params }) => {
        requests.push(`RESET-2FA ${String(params.userId)} ${JSON.stringify(await request.json())}`)
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { result, queryClient } = await withSetup(() => useUsers())
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    await result.update.mutateAsync({
      id: 'user-1',
      body: {
        firstName: 'Ada',
        lastName: 'King',
        email: null,
        phone: null,
        secondaryPhone: null,
        birthDate: null,
        nationalId: '12345678Z',
        promotionalConsent: true,
        gender: 'Female',
        parentId: null,
        currentPassword: null,
      },
    })
    await result.remove.mutateAsync('user-2')
    await result.changeType.mutateAsync({ id: 'user-1', userTypeId: 'type-member' })
    await result.setAdmin.mutateAsync({ id: 'user-1', isAdmin: true, currentPassword: 'secret' })
    await result.resetTwoFactor.mutateAsync({ id: 'user-1', currentPassword: 'secret' })

    expect(requests).toEqual([
      'PUT {"firstName":"Ada","lastName":"King","email":null,"phone":null,"secondaryPhone":null,"birthDate":null,"nationalId":"12345678Z","promotionalConsent":true,"gender":"Female","parentId":null,"currentPassword":null}',
      'DELETE user-2',
      'TYPE ?userTypeId=type-member',
      'ADMIN {"isAdmin":true,"currentPassword":"secret"}',
      'RESET-2FA user-1 {"currentPassword":"secret"}',
    ])
    expect(invalidate).toHaveBeenCalledTimes(5)
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['users'] })
  })
})
