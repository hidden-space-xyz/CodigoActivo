import { flushPromises } from '@vue/test-utils'
import { ElSelect } from 'element-plus'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { UsersPage } from '@/pages/admin/users'
import type { UserResponse } from '@/shared/api/generated/models'
import { formatDate } from '@/shared/lib/date'
import { ColumnFilterDate, ColumnFilterSelect, ColumnSearch } from '@/shared/ui/column-filter'
import { SearchInput } from '@/shared/ui/search-input'

import { renderApp, renderWithProviders, t } from '../../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../../support/server'
import {
  buildUserResponse,
  omit,
  userStatusTypes,
  userTypes,
} from '../../../../../support/builders'
import {
  acceptMessageBox,
  cancelMessageBox,
  click,
  dismissDialog,
  expectNotification,
  findButton,
  inputValue,
  isDialogOpen,
  messageBox,
  openDialog,
  openRowMenu,
  queryOf,
  rowMenuItem,
  rowMenuLabels,
  textOf,
  tp,
  typeInto,
} from '../../../../../support/dom'

const TYPE_TITLE = 'pages.admin.users.typeDialog.header'
const EMAIL_TITLE = 'features.sendEmail.header'
const GRANT_TITLE = 'pages.admin.users.grantAdmin.header'
const RESET_2FA_TITLE = 'pages.admin.users.resetTwoFactor.header'
const CHANGE_TYPE = 'pages.admin.users.actions.changeType'
const SEND_EMAIL = 'pages.admin.users.actions.sendEmail'
const RESET_2FA = 'pages.admin.users.actions.resetTwoFactor'
const GRANT_ADMIN = 'pages.admin.users.actions.grantAdmin'
const REVOKE_ADMIN = 'pages.admin.users.actions.revokeAdmin'

const ada = buildUserResponse({
  dependentCount: 2,
  promotionalConsent: true,
  secondaryPhone: '611111111',
})
const tim = omit(
  buildUserResponse({
    id: 'child-1',
    firstName: 'Tim',
    email: null,
    phone: null,
    birthDate: '2016-02-01',
    nationalId: null,
    gender: 'Male',
    parentId: 'user-1',
    parentName: 'Ada Lovelace',
  }),
  'type',
)
const grace = buildUserResponse({
  id: 'user-3',
  firstName: 'Grace',
  lastName: 'Hopper',
  isAdmin: true,
})

function ghost(overrides: Partial<UserResponse> = {}): UserResponse {
  return buildUserResponse({
    id: 'ghost-1',
    firstName: 'Ghost',
    lastName: 'User',
    email: 'ghost@example.test',
    birthDate: 'March 5, 2000',
    dependentCount: 1,
    ...overrides,
  })
}

interface Recorded {
  list: string[]
}

function serveUsers(pages: UserResponse[][] | UserResponse[] = [ada, tim]): Recorded {
  const recorded: Recorded = { list: [] }
  const allPages = Array.isArray(pages[0]) ? (pages as UserResponse[][]) : [pages as UserResponse[]]
  const total = allPages.reduce((sum, page) => sum + page.length, 0)
  server.use(
    http.get('/api/users', ({ request }) => {
      recorded.list.push(request.url)
      const page = Number(new URL(request.url).searchParams.get('page') ?? '1')
      return HttpResponse.json({ items: allPages[page - 1] ?? [], total })
    }),
    http.get('/api/users/types', () => HttpResponse.json(userTypes)),
    http.get('/api/users/status-types', () => HttpResponse.json(userStatusTypes)),
    http.get('/api/emails/users/audience', () =>
      HttpResponse.json({ recipients: 1, withoutConsent: 0 }),
    ),
  )
  return recorded
}

function rows(wrapper: Awaited<ReturnType<typeof renderPage>>['wrapper']) {
  return wrapper.findAll('.el-table__body tr')
}

function rowElement(
  wrapper: Awaited<ReturnType<typeof renderPage>>['wrapper'],
  index: number,
): HTMLElement {
  const row = rows(wrapper)[index]
  if (!row) throw new Error(`Row ${index} not rendered`)
  return row.element as HTMLElement
}

async function renderPage(user?: { id: string; isAdmin: boolean }) {
  return renderWithProviders(UsersPage, { attach: true, ...(user ? { user } : {}) })
}

type PageWrapper = Awaited<ReturnType<typeof renderPage>>['wrapper']

async function chooseRowAction(wrapper: PageWrapper, index: number, key: string): Promise<void> {
  await click(rowMenuItem(await openRowMenu(rowElement(wrapper, index)), t(key)))
}

function selectFilter(wrapper: PageWrapper, labelKey: string) {
  const filter = wrapper
    .findAllComponents(ColumnFilterSelect)
    .find((candidate) => candidate.props('label') === t(labelKey))
  if (!filter) throw new Error(`Select filter "${labelKey}" not rendered`)
  return filter
}

function chipLabels(wrapper: PageWrapper): string[] {
  return wrapper.findAll('.filter-chip__label').map((chip) => textOf(chip.element))
}

async function chooseScope(wrapper: PageWrapper, labelKey: string): Promise<void> {
  const item = wrapper
    .findAll('.el-segmented__item')
    .find((candidate) => candidate.text() === t(labelKey))
  if (!item) throw new Error(`Scope "${labelKey}" not found`)
  await click(item.find('input').element)
}

function selectedScope(wrapper: PageWrapper): string {
  return textOf(wrapper.find('.el-segmented__item.is-selected').element)
}

describe('admin users page', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 0, 15, 12))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('lists users with contact, age, catalog tags and family links', async () => {
    const recorded = serveUsers([ada, tim, ghost({ dependentCount: 0, email: '', isAdmin: true })])

    const { wrapper } = await renderPage()

    expect(queryOf(recorded.list[0] ?? '')).toEqual({
      page: '1',
      pageSize: '25',
      sort: 'firstName',
      scope: 'Independent',
    })
    const [first, second, third] = rows(wrapper)
    expect(first?.text()).toContain('Ada Lovelace')
    expect(first?.text()).toContain('ada@example.test')
    expect(first?.text()).toContain('12345678Z')
    expect(first?.text()).not.toContain('(')
    expect(first?.find('.color-tag--dot').text()).toBe('Active')
    expect(first?.text()).toContain('Participant')
    expect(first?.find('.table-primary-cell__badges').exists()).toBe(false)
    expect(textOf(first?.find('.table-primary-cell__meta').element)).toBe(
      t('pages.admin.users.dependentsLabel', { count: 2 }),
    )
    expect(first?.find('.row-actions__more').attributes('aria-label')).toBe(
      t('table.moreActionsFor', { name: 'Ada Lovelace' }),
    )
    expect(rowMenuLabels(await openRowMenu(rowElement(wrapper, 0)))).toEqual([
      t(CHANGE_TYPE),
      t(SEND_EMAIL),
      t(RESET_2FA),
      t(GRANT_ADMIN),
      t('common.delete'),
    ])

    expect(second?.text()).toContain('Tim Lovelace')
    expect(second?.find('.table-primary-cell__badges').exists()).toBe(false)
    expect(second?.findAll('.user-meta > *').map((part) => textOf(part.element))).toEqual([
      t('pages.admin.users.guardianPrefix'),
      'Ada Lovelace',
      '·',
      tp('pages.admin.users.age', 9),
    ])
    expect(second?.find('.user-meta__age').attributes('title')).toBe(formatDate('2016-02-01'))
    expect(second?.find('button').text()).toContain('Ada Lovelace')
    expect(rowMenuLabels(await openRowMenu(rowElement(wrapper, 1)))).toEqual([
      t(CHANGE_TYPE),
      t('common.delete'),
    ])

    expect(third?.text()).toContain('Ghost User')
    expect(third?.find('.table-primary-cell__badges').text()).toBe(
      t('pages.admin.users.columns.admin'),
    )
    expect(third?.text()).not.toContain('(')
    expect(third?.text()).toContain('—')
    expect(third?.find('.table-primary-cell__meta').exists()).toBe(false)
  })

  it('renders through the application router for admins', async () => {
    serveUsers()

    const { wrapper } = await renderApp('/admin/users', { user: { isAdmin: true } })

    await vi.waitFor(() => expect(wrapper.text()).toContain('Tim Lovelace'))
  })

  it('shows empty and error states and disables bulk actions without users', async () => {
    serveUsers([])
    const empty = await renderPage()
    expect(empty.wrapper.text()).toContain(t('pages.admin.users.empty.noIndependent'))
    expect(findButton(t('pages.admin.users.email.bulkLabel')).disabled).toBe(true)
    await chooseScope(empty.wrapper, 'pages.admin.users.scope.dependent')
    await vi.waitFor(() =>
      expect(empty.wrapper.text()).toContain(t('pages.admin.users.empty.noDependent')),
    )
    await chooseScope(empty.wrapper, 'pages.admin.users.scope.all')
    await vi.waitFor(() =>
      expect(empty.wrapper.text()).toContain(t('pages.admin.users.empty.none')),
    )
    empty.wrapper.unmount()

    server.use(http.get('/api/users', () => apiError(500)))
    const failed = await renderPage()
    await vi.waitFor(() =>
      expect(failed.wrapper.text()).toContain(t('pages.admin.users.empty.error')),
    )
  })

  it('sends column filters and exposes catalog options', async () => {
    const recorded = serveUsers()
    const { wrapper } = await renderPage()
    await chooseScope(wrapper, 'pages.admin.users.scope.all')

    const searches = wrapper.findAllComponents(ColumnSearch)
    expect(searches.map((search) => search.props('label'))).toEqual([
      t('common.email'),
      t('common.phone'),
      t('pages.admin.users.columns.nationalId'),
    ])
    const [email, phone, nationalId] = searches
    wrapper.findComponent(SearchInput).vm.$emit('update:modelValue', 'ada')
    email?.vm.$emit('update:modelValue', 'example')
    phone?.vm.$emit('update:modelValue', '600')
    nationalId?.vm.$emit('update:modelValue', 'x123')
    const birth = wrapper.findComponent(ColumnFilterDate)
    expect(birth.props('label')).toBe(t('pages.admin.users.columns.birth'))
    expect(birth.props('variant')).toBe('toolbar')
    birth.vm.$emit('update:modelValue', [new Date(1980, 0, 1), new Date(2000, 11, 31)])
    const consent = selectFilter(wrapper, 'pages.admin.users.columns.promotionalConsent')
    const status = selectFilter(wrapper, 'common.status')
    const type = selectFilter(wrapper, 'pages.admin.users.columns.type')
    const admin = selectFilter(wrapper, 'pages.admin.users.columns.admin')
    expect([admin, consent, status, type].map((filter) => filter.props('variant'))).toEqual([
      'toolbar',
      'toolbar',
      'header',
      'header',
    ])
    consent.vm.$emit('update:modelValue', true)
    status.vm.$emit('update:modelValue', 'status-active')
    type.vm.$emit('update:modelValue', 'type-member')
    admin.vm.$emit('update:modelValue', false)
    await flushPromises()

    expect(queryOf(recorded.list.at(-1) ?? '')).toEqual({
      page: '1',
      pageSize: '25',
      sort: 'firstName',
      name: 'ada',
      email: 'example',
      phone: '600',
      nationalId: 'x123',
      promotionalConsent: 'true',
      birthDateFrom: '1980-01-01',
      birthDateTo: '2000-12-31',
      userStatusTypeId: 'status-active',
      userTypeId: 'type-member',
      isAdmin: 'false',
    })
    expect(status.props('options')).toEqual([
      { label: 'Active', value: 'status-active' },
      { label: 'Blocked', value: 'status-blocked' },
    ])
    expect(consent.props('options')).toEqual(admin.props('options'))
    expect(admin.props('options')).toEqual([
      { label: t('common.yes'), value: true },
      { label: t('common.no'), value: false },
    ])
    expect(chipLabels(wrapper)).toHaveLength(8)
    expect(chipLabels(wrapper)).toContain(
      t('table.filterChip', {
        label: t('pages.admin.users.columns.promotionalConsent'),
        value: t('common.yes'),
      }),
    )
  })

  it('narrows the table to a tutor or to dependents and clears the relation filter', async () => {
    const recorded = serveUsers([ada, tim])
    const { wrapper } = await renderPage()
    wrapper.findComponent(SearchInput).vm.$emit('update:modelValue', 'lov')
    await flushPromises()
    expect(queryOf(recorded.list.at(-1) ?? '')).toMatchObject({ name: 'lov' })

    await click(findButton('Ada Lovelace', rowElement(wrapper, 1)))
    const tutorOf = t('pages.admin.users.relation.tutorOf', { fullName: 'Tim Lovelace' })
    expect(chipLabels(wrapper)).toEqual([tutorOf])
    expect(wrapper.find<HTMLInputElement>('.data-table__search input').element.value).toBe('')
    expect(queryOf(recorded.list.at(-1) ?? '')).toEqual({
      page: '1',
      pageSize: '25',
      sort: 'firstName',
      scope: 'Independent',
      id: 'user-1',
    })

    await click(
      findButton(t('pages.admin.users.dependentsLabel', { count: 2 }), rowElement(wrapper, 0)),
    )
    const dependentsOf = t('pages.admin.users.relation.dependentsOf', { fullName: 'Ada Lovelace' })
    expect(chipLabels(wrapper)).toEqual([dependentsOf])
    expect(selectedScope(wrapper)).toBe(t('pages.admin.users.scope.dependent'))
    expect(queryOf(recorded.list.at(-1) ?? '')).toMatchObject({
      scope: 'Dependent',
      parentId: 'user-1',
    })

    await click(findButton(t('table.removeFilter', { label: dependentsOf })))
    expect(wrapper.find('.filter-chip').exists()).toBe(false)
    expect(queryOf(recorded.list.at(-1) ?? '')).toEqual({
      page: '1',
      pageSize: '25',
      sort: 'firstName',
      scope: 'Dependent',
    })
  })

  it('switches between independent accounts, dependents and everyone', async () => {
    const recorded = serveUsers([ada, tim])
    const { wrapper } = await renderPage()

    const segmented = wrapper.find('.el-segmented')
    expect(segmented.attributes('aria-label')).toBe(t('pages.admin.users.scope.label'))
    expect(segmented.findAll('.el-segmented__item').map((item) => item.text())).toEqual([
      t('pages.admin.users.scope.independent'),
      t('pages.admin.users.scope.dependent'),
      t('pages.admin.users.scope.all'),
    ])
    expect(selectedScope(wrapper)).toBe(t('pages.admin.users.scope.independent'))
    expect(wrapper.findComponent(ColumnFilterDate).exists()).toBe(false)
    selectFilter(wrapper, 'common.status').vm.$emit('update:modelValue', 'status-active')
    await flushPromises()
    expect(chipLabels(wrapper)).toHaveLength(1)

    await chooseScope(wrapper, 'pages.admin.users.scope.dependent')
    await vi.waitFor(() =>
      expect(queryOf(recorded.list.at(-1) ?? '')).toEqual({
        page: '1',
        pageSize: '25',
        sort: 'firstName',
        scope: 'Dependent',
      }),
    )
    expect(selectedScope(wrapper)).toBe(t('pages.admin.users.scope.dependent'))
    expect(wrapper.find('.filter-chip').exists()).toBe(false)
    expect(wrapper.findAllComponents(ColumnSearch)).toHaveLength(0)
    expect(
      wrapper.findAllComponents(ColumnFilterSelect).map((filter) => filter.props('label')),
    ).toEqual([t('pages.admin.users.columns.type')])
    expect(wrapper.findComponent(ColumnFilterDate).exists()).toBe(true)
    expect(rowElement(wrapper, 0).textContent).not.toContain('ada@example.test')

    await click(findButton('Ada Lovelace', rowElement(wrapper, 1)))
    expect(selectedScope(wrapper)).toBe(t('pages.admin.users.scope.independent'))
    expect(wrapper.findComponent(ColumnFilterDate).exists()).toBe(false)
    expect(wrapper.findAllComponents(ColumnSearch)).toHaveLength(3)
    expect(selectFilter(wrapper, 'common.status').exists()).toBe(true)
    expect(selectFilter(wrapper, 'pages.admin.users.columns.admin').exists()).toBe(true)
    expect(rowElement(wrapper, 0).textContent).toContain('ada@example.test')

    await chooseScope(wrapper, 'pages.admin.users.scope.all')
    await vi.waitFor(() =>
      expect(queryOf(recorded.list.at(-1) ?? '')).toEqual({
        page: '1',
        pageSize: '25',
        sort: 'firstName',
      }),
    )
    expect(wrapper.find('.filter-chip').exists()).toBe(false)
  })

  it('closes the type and email dialogs without saving', async () => {
    serveUsers([ada])
    const { wrapper } = await renderPage()

    await chooseRowAction(wrapper, 0, CHANGE_TYPE)
    await dismissDialog(wrapper, t(TYPE_TITLE))
    await vi.waitFor(() => expect(isDialogOpen(t(TYPE_TITLE))).toBe(false))

    await chooseRowAction(wrapper, 0, SEND_EMAIL)
    await click(findButton(t('common.cancel'), openDialog(t(EMAIL_TITLE))))
    await vi.waitFor(() => expect(isDialogOpen(t(EMAIL_TITLE))).toBe(false))

    expect(document.body.querySelectorAll('.el-notification')).toHaveLength(0)
  })

  it('changes the user type', async () => {
    serveUsers([ada])
    const changes: Record<string, string>[] = []
    let fail = false
    server.use(
      http.patch('/api/users/:userId/change-type', ({ request, params }) => {
        if (fail) return apiError(500)
        changes.push({ id: String(params.userId), ...queryOf(request.url) })
        return HttpResponse.json(buildUserResponse())
      }),
    )
    const { wrapper } = await renderPage()

    await chooseRowAction(wrapper, 0, CHANGE_TYPE)
    const dialog = openDialog(t(TYPE_TITLE))
    expect(dialog.textContent).toContain(
      t('pages.admin.users.typeDialog.lead', { fullName: 'Ada Lovelace' }),
    )
    const select = wrapper
      .findAllComponents(ElSelect)
      .find(
        (candidate) =>
          candidate.attributes('id') === 'user-type' ||
          candidate.props('modelValue') === 'type-participant',
      )
    expect(select?.props('modelValue')).toBe('type-participant')
    select?.vm.$emit('update:modelValue', 'type-member')
    await flushPromises()
    await click(findButton(t('common.apply'), dialog))

    await expectNotification(t('pages.admin.users.toasts.typeUpdated'))
    expect(changes).toEqual([{ id: 'user-1', userTypeId: 'type-member' }])
    await vi.waitFor(() => expect(isDialogOpen(t(TYPE_TITLE))).toBe(false))

    fail = true
    await chooseRowAction(wrapper, 0, CHANGE_TYPE)
    await click(findButton(t('common.apply'), openDialog(t(TYPE_TITLE))))
    await expectNotification(t('common.error'))
    await click(findButton(t('common.cancel'), openDialog(t(TYPE_TITLE))))
    await vi.waitFor(() => expect(isDialogOpen(t(TYPE_TITLE))).toBe(false))
  })

  it('cannot apply a type change without a selected type', async () => {
    serveUsers([tim])
    const { wrapper } = await renderPage()

    await chooseRowAction(wrapper, 0, CHANGE_TYPE)
    const dialog = openDialog(t(TYPE_TITLE))
    expect(findButton(t('common.apply'), dialog).disabled).toBe(true)
    await click(findButton(t('common.apply'), dialog))
    await flushPromises()

    expect(isDialogOpen(t(TYPE_TITLE))).toBe(true)
    expect(document.body.querySelectorAll('.el-notification')).toHaveLength(0)
  })

  it('asks for the admin password before granting the role', async () => {
    serveUsers([ada])
    const requests: unknown[] = []
    server.use(
      http.patch('/api/users/:userId/admin', async ({ request, params }) => {
        requests.push({ id: params.userId, body: await request.json() })
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper } = await renderPage()

    await chooseRowAction(wrapper, 0, GRANT_ADMIN)
    const dialog = openDialog(t(GRANT_TITLE))
    expect(dialog.textContent).toContain(
      t('pages.admin.users.grantAdmin.message', { fullName: 'Ada Lovelace' }),
    )
    expect(requests).toEqual([])

    await click(findButton(t('pages.admin.users.grantAdmin.confirm'), dialog))
    expect(dialog.textContent).toContain(
      t('pages.admin.users.adminPassword.form.problems.passwordRequired'),
    )
    expect(requests).toEqual([])

    await typeInto('#grant-admin-password', 'Str0ngPass!23')
    await click(findButton(t('pages.admin.users.grantAdmin.confirm'), dialog))

    await expectNotification(t('pages.admin.users.toasts.adminGranted'))
    expect(requests).toEqual([
      { id: 'user-1', body: { isAdmin: true, currentPassword: 'Str0ngPass!23' } },
    ])
    await vi.waitFor(() => expect(isDialogOpen(t(GRANT_TITLE))).toBe(false))
  })

  it('keeps the grant dialog open when the password is rejected or the request fails', async () => {
    serveUsers([ada])
    let rejectPassword = true
    server.use(
      http.patch('/api/users/:userId/admin', () =>
        rejectPassword ? apiError(400, 'UserCurrentPasswordIncorrect') : apiError(500),
      ),
    )
    const { wrapper } = await renderPage()

    await chooseRowAction(wrapper, 0, GRANT_ADMIN)
    const dialog = openDialog(t(GRANT_TITLE))
    await typeInto('#grant-admin-password', 'wrong-password')
    await click(findButton(t('pages.admin.users.grantAdmin.confirm'), dialog))

    await vi.waitFor(() =>
      expect(dialog.textContent).toContain(t('errors.UserCurrentPasswordIncorrect')),
    )
    expect(document.body.querySelectorAll('.el-notification')).toHaveLength(0)
    expect(isDialogOpen(t(GRANT_TITLE))).toBe(true)
    expect(rowElement(wrapper, 0).querySelector('.table-primary-cell__badges')).toBeNull()

    rejectPassword = false
    await click(findButton(t('pages.admin.users.grantAdmin.confirm'), dialog))
    await expectNotification(t('common.error'))
    expect(dialog.textContent).not.toContain(t('errors.UserCurrentPasswordIncorrect'))
    expect(isDialogOpen(t(GRANT_TITLE))).toBe(true)

    await click(findButton(t('common.cancel'), dialog))
    await vi.waitFor(() => expect(isDialogOpen(t(GRANT_TITLE))).toBe(false))

    await chooseRowAction(wrapper, 0, GRANT_ADMIN)
    expect(inputValue('#grant-admin-password')).toBe('')
    await dismissDialog(wrapper, t(GRANT_TITLE))
    await vi.waitFor(() => expect(isDialogOpen(t(GRANT_TITLE))).toBe(false))
  })

  it('revokes the admin role after confirmation without asking for a password', async () => {
    serveUsers([grace])
    const requests: unknown[] = []
    let fail = false
    server.use(
      http.patch('/api/users/:userId/admin', async ({ request, params }) => {
        if (fail) return apiError(500)
        requests.push({ id: params.userId, body: await request.json() })
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper } = await renderPage()

    await chooseRowAction(wrapper, 0, REVOKE_ADMIN)
    expect(messageBox().textContent).toContain(
      t('pages.admin.users.revokeAdmin.message', { fullName: 'Grace Hopper' }),
    )
    await cancelMessageBox()
    await flushPromises()
    expect(requests).toEqual([])
    expect(isDialogOpen(t(GRANT_TITLE))).toBe(false)

    await chooseRowAction(wrapper, 0, REVOKE_ADMIN)
    await acceptMessageBox()
    await expectNotification(t('pages.admin.users.toasts.adminRevoked'))
    expect(requests).toEqual([{ id: 'user-3', body: { isAdmin: false, currentPassword: null } }])

    fail = true
    await chooseRowAction(wrapper, 0, REVOKE_ADMIN)
    await acceptMessageBox()
    await expectNotification(t('common.error'))
  })

  it('locks the role and the deletion of the initial administrator', async () => {
    serveUsers([buildUserResponse({ isAdmin: true, isInitialAdmin: true }), grace])
    const { wrapper } = await renderPage()
    const blocked = t('pages.admin.users.blocked.initialAdmin')

    const initialMenu = await openRowMenu(rowElement(wrapper, 0))
    const initialRevoke = rowMenuItem(initialMenu, t(REVOKE_ADMIN))
    const initialDelete = rowMenuItem(initialMenu, t('common.delete'))
    expect(initialRevoke.getAttribute('aria-disabled')).toBe('true')
    expect(textOf(initialRevoke.querySelector('.row-actions__item-reason'))).toBe(blocked)
    expect(initialDelete.getAttribute('aria-disabled')).toBe('true')
    expect(textOf(initialDelete.querySelector('.row-actions__item-reason'))).toBe(blocked)
    await click(initialDelete)
    expect(document.body.querySelector('.el-message-box')).toBeNull()

    const graceMenu = await openRowMenu(rowElement(wrapper, 1))
    const graceDelete = rowMenuItem(graceMenu, t('common.delete'))
    expect(rowMenuItem(graceMenu, t(REVOKE_ADMIN)).getAttribute('aria-disabled')).not.toBe('true')
    expect(graceDelete.getAttribute('aria-disabled')).not.toBe('true')
    expect(graceDelete.querySelector('.row-actions__item-reason')).toBeNull()
  })

  it('marks the own account and keeps its type, admin role and deletion out of reach', async () => {
    const requests: string[] = []
    serveUsers([
      buildUserResponse({ isAdmin: true }),
      buildUserResponse({ id: 'user-2', firstName: 'Grace', lastName: 'Hopper' }),
    ])
    server.use(
      http.patch('/api/users/:userId/admin', ({ params }) => {
        requests.push(String(params.userId))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper } = await renderPage({ id: 'user-1', isAdmin: true })
    const [own, other] = rows(wrapper)

    expect(textOf(own?.find('.table-primary-cell__badges').element)).toContain(
      t('pages.admin.users.badges.you'),
    )
    expect(other?.find('.table-primary-cell__badges').exists()).toBe(false)

    const ownMenu = await openRowMenu(rowElement(wrapper, 0))
    const ownType = rowMenuItem(ownMenu, t(CHANGE_TYPE))
    const ownRevoke = rowMenuItem(ownMenu, t(REVOKE_ADMIN))
    const ownDelete = rowMenuItem(ownMenu, t('common.delete'))
    for (const item of [ownType, ownRevoke]) {
      expect(item.getAttribute('aria-disabled')).toBe('true')
      expect(textOf(item.querySelector('.row-actions__item-reason'))).toBe(
        t('pages.admin.users.blocked.selfRole'),
      )
    }
    expect(ownDelete.getAttribute('aria-disabled')).toBe('true')
    expect(textOf(ownDelete.querySelector('.row-actions__item-reason'))).toBe(
      t('pages.admin.users.blocked.self'),
    )
    await click(ownType)
    await click(ownRevoke)
    await flushPromises()
    expect(isDialogOpen(t(TYPE_TITLE))).toBe(false)
    expect(document.body.querySelector('.el-message-box')).toBeNull()
    expect(requests).toEqual([])

    const otherMenu = await openRowMenu(rowElement(wrapper, 1))
    for (const key of [CHANGE_TYPE, GRANT_ADMIN, 'common.delete']) {
      expect(rowMenuItem(otherMenu, t(key)).getAttribute('aria-disabled')).not.toBe('true')
    }
  })

  it('deletes a user after confirmation and reports failures', async () => {
    serveUsers([ada])
    const deleted: string[] = []
    let fail = false
    server.use(
      http.delete('/api/users/:userId', ({ params }) => {
        if (fail) return apiError(500)
        deleted.push(String(params.userId))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper } = await renderPage()

    await chooseRowAction(wrapper, 0, 'common.delete')
    expect(messageBox().textContent).toContain(
      t('pages.admin.users.delete.message', { fullName: 'Ada Lovelace' }),
    )
    await acceptMessageBox()
    await expectNotification(t('pages.admin.users.toasts.deleted'))
    expect(deleted).toEqual(['user-1'])

    fail = true
    await chooseRowAction(wrapper, 0, 'common.delete')
    await acceptMessageBox()
    await expectNotification(t('common.error'))
  })

  it('queues an email for all filtered users after confirmation', async () => {
    serveUsers([ada, tim])
    let received: { url: string; subject: unknown; purpose: unknown } | undefined
    const audienceUrls: string[] = []
    server.use(
      http.post('/api/emails/users', async ({ request }) => {
        const form = await request.formData()
        received = { url: request.url, subject: form.get('subject'), purpose: form.get('purpose') }
        return HttpResponse.json({ queued: 2 })
      }),
      http.get('/api/emails/users/audience', ({ request }) => {
        audienceUrls.push(request.url)
        return HttpResponse.json({ recipients: 2, withoutConsent: 1 })
      }),
    )
    const { wrapper } = await renderPage()
    await click(findButton(t('pages.admin.users.dependentsLabel', { count: 2 })))

    await click(findButton(t('pages.admin.users.email.bulkLabel')))
    const dialog = openDialog(t(EMAIL_TITLE))
    expect(dialog.textContent).toContain(tp('pages.admin.users.email.targetFiltered', 2, { n: 2 }))
    await vi.waitFor(() =>
      expect(dialog.textContent).toContain(tp('features.sendEmail.recipients', 2, { count: 2 })),
    )
    expect(audienceUrls.map(queryOf)).toEqual([{ scope: 'Dependent', parentId: 'user-1' }])
    await click(dialog.querySelector('input[type="radio"][value="Promotional"]')!)
    expect(dialog.querySelector('.el-alert--info')?.textContent).toContain(
      tp('features.sendEmail.withoutConsent', 1, { count: 1 }),
    )
    await typeInto('#send-email-subject', 'News')
    await typeInto('#send-email-body', 'Hello everyone')
    await click(findButton(t('features.sendEmail.send'), dialog))
    await acceptMessageBox()

    await expectNotification(tp('features.sendEmail.toast.queued', 2, { count: 2 }))
    expect(received?.subject).toBe('News')
    expect(received?.purpose).toBe('Promotional')
    expect(queryOf(received?.url ?? '')).toEqual({ scope: 'Dependent', parentId: 'user-1' })
    await vi.waitFor(() => expect(isDialogOpen(t(EMAIL_TITLE))).toBe(false))
    expect(chipLabels(wrapper)).toEqual([
      t('pages.admin.users.relation.dependentsOf', { fullName: 'Ada Lovelace' }),
    ])
  })

  it('queues an email for a single user and reports failures', async () => {
    serveUsers([ada])
    const sentTo: string[] = []
    const audienceUrls: string[] = []
    let fail = false
    server.use(
      http.post('/api/emails/users/:userId', ({ params }) => {
        if (fail) return apiError(500)
        sentTo.push(String(params.userId))
        return HttpResponse.json({ queued: 1 })
      }),
      http.get('/api/emails/users/audience', ({ request }) => {
        audienceUrls.push(request.url)
        return HttpResponse.json({ recipients: 1, withoutConsent: 0 })
      }),
    )
    const { wrapper } = await renderPage()

    await chooseRowAction(wrapper, 0, SEND_EMAIL)
    let dialog = openDialog(t(EMAIL_TITLE))
    expect(dialog.textContent).toContain('Ada Lovelace')
    await vi.waitFor(() => expect(audienceUrls.map(queryOf)).toEqual([{ id: 'user-1' }]))
    await flushPromises()
    expect(dialog.querySelector('.el-alert')).toBeNull()
    await click(dialog.querySelector('input[type="radio"][value="Informational"]')!)
    await typeInto('#send-email-subject', 'Hi')
    await typeInto('#send-email-body', 'Personal note')
    await click(findButton(t('features.sendEmail.send'), dialog))
    await acceptMessageBox()
    await expectNotification(tp('features.sendEmail.toast.queued', 1, { count: 1 }))
    expect(sentTo).toEqual(['user-1'])

    fail = true
    await chooseRowAction(wrapper, 0, SEND_EMAIL)
    dialog = openDialog(t(EMAIL_TITLE))
    await click(dialog.querySelector('input[type="radio"][value="Informational"]')!)
    await typeInto('#send-email-subject', 'Hi')
    await typeInto('#send-email-body', 'Again')
    await click(findButton(t('features.sendEmail.send'), dialog))
    await acceptMessageBox()
    await expectNotification(t('common.error'))
    expect(isDialogOpen(t(EMAIL_TITLE))).toBe(true)
  })

  it('warns that resetting drops the authenticator app of a user who has one', async () => {
    serveUsers([buildUserResponse({ ...ada, twoFactorMethod: 'Authenticator' })])
    const { wrapper } = await renderPage()

    await chooseRowAction(wrapper, 0, RESET_2FA)

    expect(openDialog(t(RESET_2FA_TITLE)).textContent).toContain(
      t('pages.admin.users.resetTwoFactor.message', { fullName: 'Ada Lovelace' }),
    )
  })

  it('resets the second factor of a user after confirming the admin password', async () => {
    serveUsers([ada])
    const requests: unknown[] = []
    server.use(
      http.post('/api/users/:userId/two-factor/reset', async ({ request, params }) => {
        requests.push({ id: params.userId, body: await request.json() })
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper } = await renderPage()

    await chooseRowAction(wrapper, 0, RESET_2FA)
    const dialog = openDialog(t(RESET_2FA_TITLE))
    expect(dialog.textContent).toContain(
      t('pages.admin.users.resetTwoFactor.messageEmail', { fullName: 'Ada Lovelace' }),
    )

    await click(findButton(t('pages.admin.users.resetTwoFactor.confirm'), dialog))
    expect(dialog.textContent).toContain(
      t('pages.admin.users.adminPassword.form.problems.passwordRequired'),
    )
    expect(requests).toEqual([])

    await typeInto('#reset-two-factor-password', 'Str0ngPass!23')
    await click(findButton(t('pages.admin.users.resetTwoFactor.confirm'), dialog))

    await expectNotification(t('pages.admin.users.toasts.twoFactorReset'))
    expect(requests).toEqual([{ id: 'user-1', body: { currentPassword: 'Str0ngPass!23' } }])
    await vi.waitFor(() => expect(isDialogOpen(t(RESET_2FA_TITLE))).toBe(false))
  })

  it('keeps the reset dialog open when the password is rejected or the request fails', async () => {
    serveUsers([ada, tim])
    let rejectPassword = true
    server.use(
      http.post('/api/users/:userId/two-factor/reset', () =>
        rejectPassword ? apiError(400, 'UserCurrentPasswordIncorrect') : apiError(500),
      ),
    )
    const { wrapper } = await renderPage()
    expect(rowMenuLabels(await openRowMenu(rowElement(wrapper, 1)))).not.toContain(t(RESET_2FA))

    await chooseRowAction(wrapper, 0, RESET_2FA)
    const dialog = openDialog(t(RESET_2FA_TITLE))
    await typeInto('#reset-two-factor-password', 'wrong-password')
    await click(findButton(t('pages.admin.users.resetTwoFactor.confirm'), dialog))

    await vi.waitFor(() =>
      expect(dialog.textContent).toContain(t('errors.UserCurrentPasswordIncorrect')),
    )
    expect(document.body.querySelectorAll('.el-notification')).toHaveLength(0)
    expect(isDialogOpen(t(RESET_2FA_TITLE))).toBe(true)

    rejectPassword = false
    await click(findButton(t('pages.admin.users.resetTwoFactor.confirm'), dialog))
    await expectNotification(t('common.error'))
    expect(dialog.textContent).not.toContain(t('errors.UserCurrentPasswordIncorrect'))
    expect(isDialogOpen(t(RESET_2FA_TITLE))).toBe(true)

    await dismissDialog(wrapper, t(RESET_2FA_TITLE))
    await vi.waitFor(() => expect(isDialogOpen(t(RESET_2FA_TITLE))).toBe(false))
  })
})
