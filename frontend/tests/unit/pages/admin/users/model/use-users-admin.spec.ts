import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { toUser } from '@/entities/user/api/mapper'
import { useUsersAdmin } from '@/pages/admin/users/model/use-users-admin'
import type { UserResponse } from '@/shared/api/generated/models'

import { apiError, http, HttpResponse, noContent, server } from '../../../../../support/server'
import {
  buildDependentResponse,
  buildUserResponse,
  userStatusTypes,
  userTypes,
} from '../../../../../support/builders'
import { expectNotification, queryOf } from '../../../../../support/dom'
import { mountComposable, t } from '../../../../../support/render'

function serveUserPages(pages: UserResponse[][]) {
  const urls: string[] = []
  const total = pages.reduce((sum, page) => sum + page.length, 0)
  server.use(
    http.get('/api/users', ({ request }) => {
      urls.push(request.url)
      const page = Number(queryOf(request.url).page ?? '1')
      return HttpResponse.json({ items: pages[page - 1] ?? [], total })
    }),
    http.get('/api/users/status-types', () => HttpResponse.json(userStatusTypes)),
    http.get('/api/users/types', () => HttpResponse.json(userTypes)),
  )
  return urls
}

describe('useUsersAdmin', () => {
  it('narrows the table to the guardian or the dependents of a user', async () => {
    const urls = serveUserPages([[buildUserResponse()]])
    const { result } = await mountComposable(() => useUsersAdmin())
    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'firstName' })

    result.showDependentsOf(toUser(buildUserResponse()))
    await flushPromises()
    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({ parentId: 'user-1' })

    result.showGuardianOf(toUser(buildDependentResponse()))
    await flushPromises()
    expect(result.table.filterParams.value).toEqual({ id: 'user-1' })
    expect(result.relationFilter.value?.label).toBe(
      t('pages.admin.users.relation.tutorOf', { fullName: 'Byron Lovelace' }),
    )

    result.clearRelationFilter()
    await flushPromises()
    expect(result.table.filterParams.value).toEqual({})
  })

  it('filters the table by promotional consent and DNI/NIE', async () => {
    const urls = serveUserPages([[buildUserResponse()]])
    const { result } = await mountComposable(() => useUsersAdmin())
    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))

    result.table.columnFilter('promotionalConsent').value = false
    result.table.columnFilter('nationalId').value = 'x123'
    await flushPromises()

    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({
      promotionalConsent: 'false',
      nationalId: 'x123',
    })
  })

  it('fetches every page with the current filters and sort for exports', async () => {
    const firstPage = Array.from({ length: 100 }, (_, index) =>
      buildUserResponse({ id: `user-${String(index)}` }),
    )
    const urls = serveUserPages([firstPage, [buildUserResponse({ id: 'user-last' })]])
    const { result } = await mountComposable(() => useUsersAdmin())
    result.table.columnFilter('isAdmin').value = true
    result.table.onSortChange({ prop: 'email', order: 'descending' })
    await flushPromises()

    const users = await result.table.fetchAll()

    expect(users).toHaveLength(101)
    expect(users.at(-1)?.id).toBe('user-last')
    expect(urls.slice(-2).map(queryOf)).toEqual([
      { isAdmin: 'true', sort: '-email', page: '1', pageSize: '100' },
      { isAdmin: 'true', sort: '-email', page: '2', pageSize: '100' },
    ])
  })

  it('keeps a refused admin password in the dialog instead of a toast', async () => {
    serveUserPages([[]])
    let refuse = true
    server.use(
      http.patch('/api/users/:userId/admin', () =>
        refuse ? apiError(400, 'UserCurrentPasswordIncorrect') : noContent(),
      ),
    )
    const { result } = await mountComposable(() => useUsersAdmin())
    const ada = toUser(buildUserResponse())

    result.toggleAdmin(ada, true)
    await flushPromises()
    result.grantAdmin('wrong')
    await vi.waitFor(() =>
      expect(result.grantError.value).toBe(t('errors.UserCurrentPasswordIncorrect')),
    )
    expect(result.grantDialog.visible.value).toBe(true)

    refuse = false
    result.grantAdmin('Str0ngPass!23')
    await expectNotification(t('pages.admin.users.toasts.adminGranted'))
    expect(result.grantDialog.visible.value).toBe(false)
  })

  it('tells an admin that a new email of their own account waits for its link', async () => {
    serveUserPages([[]])
    server.use(
      http.get('/api/users/:userId', ({ params }) =>
        HttpResponse.json(buildUserResponse({ id: String(params.userId) })),
      ),
      http.put('/api/users/:userId', () => HttpResponse.json(buildUserResponse())),
    )
    const { result } = await mountComposable(() => useUsersAdmin(), {
      user: { id: 'user-1', isAdmin: true },
    })
    const input = {
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: ' Ada.New@example.test ',
      phone: '600000000',
      secondaryPhone: null,
      birthDate: null,
      nationalId: '12345678Z',
      promotionalConsent: false,
      gender: 'Female',
      parentId: null,
      currentPassword: 'Str0ngPass!23',
    } as const

    await result.openEdit(toUser(buildUserResponse()))
    result.saveUser(input)
    await expectNotification(t('pages.admin.users.toasts.emailChangeSent', { email: input.email }))

    await result.openEdit(toUser(buildUserResponse({ id: 'user-2' })))
    result.saveUser(input)
    await expectNotification(t('pages.admin.users.toasts.updated'))
  })
})
