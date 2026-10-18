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
import {
  acceptMessageBox,
  cancelMessageBox,
  expectNotification,
  messageBox,
  queryOf,
  textOf,
} from '../../../../../support/dom'
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
    await flushPromises()
    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({
      userStatusTypeId: 'status-active',
      email: 'ada',
      sort: '-email',
    })

    result.setScope('dependent')
    await flushPromises()
    expect(result.scope.value).toBe('dependent')
    expect(result.showsField('email')).toBe(false)
    expect(result.showsField('birthDate')).toBe(true)
    expect(queryOf(urls.at(-1) ?? '')).toEqual({
      page: '1',
      pageSize: '25',
      sort: 'firstName',
      scope: 'Dependent',
      userTypeId: 'type-member',
    })

    result.table.onSortChange({ prop: 'type', order: 'descending' })
    result.setScope('dependent')
    await flushPromises()
    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({ sort: '-type' })

    result.table.columnFilter('birthDate').value = ['2015-01-01', '2016-12-31']
    await flushPromises()
    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({ birthDateFrom: '2015-01-01' })
    result.setScope('independent')
    await flushPromises()
    expect(queryOf(urls.at(-1) ?? '')).toEqual({
      page: '1',
      pageSize: '25',
      sort: '-type',
      scope: 'Independent',
      userTypeId: 'type-member',
    })

    result.showGuardianOf(toUser(buildDependentResponse()))
    await flushPromises()
    result.setScope('all')
    await flushPromises()
    expect(['email', 'birthDate'].map((field) => result.showsField(field))).toEqual([true, true])
    expect(result.relationChips.value).toEqual([])
    expect(queryOf(urls.at(-1) ?? '')).toEqual({ page: '1', pageSize: '25', sort: '-type' })
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

  it('revokes the admin role only after confirmation', async () => {
    serveUserPages([[]])
    const requests: unknown[] = []
    server.use(
      http.patch('/api/users/:userId/admin', async ({ request, params }) => {
        requests.push({ id: params.userId, body: await request.json() })
        return noContent()
      }),
    )
    const { result, router } = await mountComposable(() => useUsersAdmin(), {
      user: { id: 'user-1', isAdmin: true },
      route: '/admin/users',
    })
    const grace = toUser(
      buildUserResponse({ id: 'user-2', firstName: 'Grace', lastName: 'Hopper', isAdmin: true }),
    )

    result.toggleAdmin(grace, false)
    await flushPromises()
    const box = messageBox()
    expect(textOf(box.querySelector('.el-message-box__title'))).toBe(
      t('pages.admin.users.revokeAdmin.header'),
    )
    expect(box.textContent).toContain(
      t('pages.admin.users.revokeAdmin.message', { fullName: 'Grace Hopper' }),
    )
    expect(textOf(box.querySelector('.el-message-box__btns .el-button--primary'))).toBe(
      t('pages.admin.users.revokeAdmin.confirm'),
    )
    await cancelMessageBox()
    await flushPromises()
    expect(requests).toEqual([])
    expect(result.grantDialog.visible.value).toBe(false)

    result.toggleAdmin(grace, false)
    await flushPromises()
    await acceptMessageBox()

    await expectNotification(t('pages.admin.users.toasts.adminRevoked'))
    expect(requests).toEqual([{ id: 'user-2', body: { isAdmin: false, currentPassword: null } }])
    expect(router.currentRoute.value.name).toBe('admin-users')
  })

  it('filters the table by news consent and DNI/NIE', async () => {
    const urls = serveUserPages([[buildUserResponse()]])
    const { result } = await mountComposable(() => useUsersAdmin())
    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))

    result.table.columnFilter('newsConsent').value = false
    result.table.columnFilter('nationalId').value = 'x123'
    await flushPromises()

    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({
      newsConsent: 'false',
      nationalId: 'x123',
    })
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
})
