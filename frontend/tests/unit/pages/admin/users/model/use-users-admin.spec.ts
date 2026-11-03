import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { toUser } from '@/entities/user/api/mapper'
import { useUsersAdmin } from '@/pages/admin/users/model/use-users-admin'
import type { UserResponse } from '@/shared/api/generated/models'

import { http, HttpResponse, server } from '../../../../../support/server'
import {
  buildDependentResponse,
  buildUserResponse,
  userStatusTypes,
  userTypes,
} from '../../../../../support/builders'
import { queryOf } from '../../../../../support/dom'
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
    expect(queryOf(urls[0] ?? '')).toEqual({
      page: '1',
      pageSize: '25',
      sort: 'firstName',
      scope: 'Independent',
    })
    expect(result.relationChips.value).toEqual([])

    result.showDependentsOf(toUser(buildUserResponse()))
    await flushPromises()
    expect(result.scope.value).toBe('dependent')
    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({ scope: 'Dependent', parentId: 'user-1' })

    result.showGuardianOf(toUser(buildDependentResponse()))
    await flushPromises()
    expect(result.scope.value).toBe('independent')
    expect(result.table.filterParams.value).toEqual({ scope: 'Independent', id: 'user-1' })
    expect(result.relationChips.value[0]?.label).toBe(
      t('pages.admin.users.relation.tutorOf', { fullName: 'Byron Lovelace' }),
    )

    result.relationChips.value[0]?.remove()
    await flushPromises()
    expect(result.table.filterParams.value).toEqual({ scope: 'Independent' })
  })

  it('switches between independent accounts, dependents and everyone', async () => {
    const urls = serveUserPages([[buildUserResponse()]])
    const { result } = await mountComposable(() => useUsersAdmin())
    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))
    expect(result.scope.value).toBe('independent')
    expect(result.showsField('email')).toBe(true)
    expect(result.showsField('birthDate')).toBe(false)
    result.table.columnFilter('status').value = 'status-active'
    result.table.columnFilter('email').value = 'ada'
    result.table.columnFilter('phone').value = '600'
    result.table.columnFilter('nationalId').value = 'x123'
    result.table.columnFilter('isAdmin').value = false
    result.table.columnFilter('newsConsent').value = true
    result.table.columnFilter('type').value = 'type-member'
    result.table.onSortChange({ prop: 'email', order: 'descending' })
    await vi.waitFor(() =>
      expect(queryOf(urls.at(-1) ?? '')).toMatchObject({
        userStatusTypeId: 'status-active',
        email: 'ada',
        sort: '-email',
      }),
    )

    result.setScope('dependent')
    expect(result.scope.value).toBe('dependent')
    expect(result.showsField('email')).toBe(false)
    expect(result.showsField('birthDate')).toBe(true)
    await vi.waitFor(() =>
      expect(queryOf(urls.at(-1) ?? '')).toEqual({
        page: '1',
        pageSize: '25',
        sort: 'firstName',
        scope: 'Dependent',
        userTypeId: 'type-member',
      }),
    )

    result.table.onSortChange({ prop: 'type', order: 'descending' })
    result.setScope('dependent')
    await vi.waitFor(() => expect(queryOf(urls.at(-1) ?? '')).toMatchObject({ sort: '-type' }))

    result.table.columnFilter('birthDate').value = ['2015-01-01', '2016-12-31']
    await vi.waitFor(() =>
      expect(queryOf(urls.at(-1) ?? '')).toMatchObject({ birthDateFrom: '2015-01-01' }),
    )
    result.setScope('independent')
    await vi.waitFor(() =>
      expect(queryOf(urls.at(-1) ?? '')).toEqual({
        page: '1',
        pageSize: '25',
        sort: '-type',
        scope: 'Independent',
        userTypeId: 'type-member',
      }),
    )

    result.showGuardianOf(toUser(buildDependentResponse()))
    await flushPromises()
    result.setScope('all')
    expect(['email', 'birthDate'].map((field) => result.showsField(field))).toEqual([true, true])
    expect(result.relationChips.value).toEqual([])
    await vi.waitFor(() =>
      expect(queryOf(urls.at(-1) ?? '')).toEqual({ page: '1', pageSize: '25', sort: '-type' }),
    )
  })

  it('lists the relation filter as a table chip that removes it', async () => {
    serveUserPages([[buildUserResponse()]])
    const { result } = await mountComposable(() => useUsersAdmin())
    result.table.columnFilter('email').value = 'ada'

    result.showDependentsOf(toUser(buildUserResponse()))
    await flushPromises()

    expect(result.table.filterParams.value).toEqual({ scope: 'Dependent', parentId: 'user-1' })
    expect(result.relationChips.value).toEqual([
      {
        key: 'relation',
        label: t('pages.admin.users.relation.dependentsOf', { fullName: 'Ada Lovelace' }),
        remove: expect.any(Function),
      },
    ])

    result.relationChips.value[0]?.remove()
    await flushPromises()

    expect(result.relationChips.value).toEqual([])
    expect(result.table.filterParams.value).toEqual({ scope: 'Dependent' })
  })

  it('explains why the initial administrator and the own account are protected', async () => {
    serveUserPages([[]])
    const own = toUser(buildUserResponse({ isAdmin: true }))
    const initial = toUser(
      buildUserResponse({ id: 'admin-0', isAdmin: true, isInitialAdmin: true }),
    )
    const other = toUser(buildUserResponse({ id: 'user-2' }))
    const signedIn = await mountComposable(() => useUsersAdmin(), {
      user: { id: 'user-1', isAdmin: true },
    })
    const anonymous = await mountComposable(() => useUsersAdmin())
    const { result } = signedIn

    expect([own, initial, other].map((user) => result.isOwnAccount(user))).toEqual([
      true,
      false,
      false,
    ])
    expect(anonymous.result.isOwnAccount(own)).toBe(false)

    expect(result.typeBlockReason(own)).toBe(t('pages.admin.users.blocked.selfRole'))
    expect(result.typeBlockReason(initial)).toBeUndefined()
    expect(result.typeBlockReason(other)).toBeUndefined()
    expect(anonymous.result.typeBlockReason(own)).toBeUndefined()

    expect(result.adminBlockReason(initial)).toBe(t('pages.admin.users.blocked.initialAdmin'))
    expect(result.adminBlockReason(own)).toBe(t('pages.admin.users.blocked.selfRole'))
    expect(result.adminBlockReason(other)).toBeUndefined()
    expect(anonymous.result.adminBlockReason(own)).toBeUndefined()

    expect(result.deleteBlockReason(initial)).toBe(t('pages.admin.users.blocked.initialAdmin'))
    expect(result.deleteBlockReason(own)).toBe(t('pages.admin.users.blocked.self'))
    expect(result.deleteBlockReason(other)).toBeUndefined()
    expect(anonymous.result.deleteBlockReason(own)).toBeUndefined()
  })
})
