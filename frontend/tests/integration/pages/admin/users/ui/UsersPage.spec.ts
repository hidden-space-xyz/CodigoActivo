import { flushPromises } from '@vue/test-utils'
import { ElSelect } from 'element-plus'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { AuthUser } from '@/entities/session'
import { UsersPage } from '@/pages/admin/users'
import StaffRoleDialog from '@/pages/admin/users/ui/StaffRoleDialog.vue'
import type { UserResponse } from '@/shared/api/generated/models'
import { formatDate } from '@/shared/lib/date'
import { ColumnFilterDate, ColumnFilterSelect, ColumnSearch } from '@/shared/ui/column-filter'
import { SearchInput } from '@/shared/ui/search-input'

import { renderApp, renderWithProviders, t } from '../../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../../support/server'
import { sessionOf } from '../../../../../support/session'
import {
  buildUserResponse,
  omit,
  userStatusTypes,
  userTypes,
} from '../../../../../support/builders'
import {
  acceptMessageBox,
  click,
  dismissDialog,
  expectNotification,
  findButton,
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
const STAFF_ROLE_TITLE = 'pages.admin.users.staffRole.header'
const OFFICER_TITLE = 'pages.admin.users.privacyOfficer.header'
const RESET_2FA_TITLE = 'pages.admin.users.resetTwoFactor.header'
const CHANGE_TYPE = 'pages.admin.users.actions.changeType'
const SEND_EMAIL = 'pages.admin.users.actions.sendEmail'
const RESET_2FA = 'pages.admin.users.actions.resetTwoFactor'
const CHANGE_STAFF_ROLE = 'pages.admin.users.actions.changeStaffRole'
const DESIGNATE_OFFICER = 'pages.admin.users.actions.designatePrivacyOfficer'
const SUPER_ADMIN = { id: 'admin-0', staffRole: 'Administrator', isSuperAdmin: true } as const

const ada = buildUserResponse({
  dependentCount: 2,
  newsConsent: true,
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
  staffRole: 'Administrator',
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

async function renderPage(user?: Partial<AuthUser>) {
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
    const recorded = serveUsers([
      ada,
      tim,
      ghost({ dependentCount: 0, email: '', staffRole: 'Administrator', isPrivacyOfficer: true }),
    ])

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
    expect(textOf(first?.find('.staff-cell').element)).toBe('—')
    expect(rowMenuLabels(await openRowMenu(rowElement(wrapper, 0)))).toEqual([
      t(CHANGE_TYPE),
      t(SEND_EMAIL),
      t(RESET_2FA),
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
    expect(rowMenuLabels(await openRowMenu(rowElement(wrapper, 1)))).toEqual([t(CHANGE_TYPE)])

    expect(third?.text()).toContain('Ghost User')
    expect(third?.find('.table-primary-cell__badges').exists()).toBe(false)
    expect(third?.findAll('.staff-cell > *').map((part) => textOf(part.element))).toEqual([
      t('entities.user.staffRole.Administrator'),
      t('pages.admin.users.badges.privacyOfficer'),
    ])
    expect(third?.find('.staff-cell .el-tag').attributes('title')).toBe(
      t('pages.admin.users.badges.privacyOfficerTitle'),
    )
    expect(third?.text()).not.toContain('(')
    expect(third?.text()).toContain('—')
    expect(third?.find('.table-primary-cell__meta').exists()).toBe(false)
  })

  it('renders through the application router for admins', async () => {
    serveUsers()

    const { wrapper } = await renderApp('/admin/users', { user: { staffRole: 'Administrator' } })

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
    const consent = selectFilter(wrapper, 'pages.admin.users.columns.newsConsent')
    const status = selectFilter(wrapper, 'common.status')
    const type = selectFilter(wrapper, 'pages.admin.users.columns.type')
    const staffRole = selectFilter(wrapper, 'pages.admin.users.columns.staffRole')
    expect([consent, status, type, staffRole].map((filter) => filter.props('variant'))).toEqual([
      'toolbar',
      'header',
      'header',
      'header',
    ])
    consent.vm.$emit('update:modelValue', true)
    status.vm.$emit('update:modelValue', 'status-active')
    type.vm.$emit('update:modelValue', 'type-member')
    staffRole.vm.$emit('update:modelValue', 'Editor')

    await vi.waitFor(() =>
      expect(queryOf(recorded.list.at(-1) ?? '')).toEqual({
        page: '1',
        pageSize: '25',
        sort: 'firstName',
        name: 'ada',
        email: 'example',
        phone: '600',
        nationalId: 'x123',
        newsConsent: 'true',
        birthDateFrom: '1980-01-01',
        birthDateTo: '2000-12-31',
        userStatusTypeId: 'status-active',
        userTypeId: 'type-member',
        staffRole: 'Editor',
      }),
    )
    expect(status.props('options')).toEqual([
      { label: 'Active', value: 'status-active' },
      { label: 'Pending', value: 'status-pending' },
    ])
    expect(consent.props('options')).toEqual([
      { label: t('common.yes'), value: true },
      { label: t('common.no'), value: false },
    ])
    expect(staffRole.props('options')).toEqual([
      { label: t('entities.user.staffRole.None'), value: 'None' },
      { label: t('entities.user.staffRole.Editor'), value: 'Editor' },
      { label: t('entities.user.staffRole.Administrator'), value: 'Administrator' },
    ])
    expect(chipLabels(wrapper)).toHaveLength(8)
    expect(chipLabels(wrapper)).toContain(
      t('table.filterChip', {
        label: t('pages.admin.users.columns.newsConsent'),
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
    await vi.waitFor(() =>
      expect(queryOf(recorded.list.at(-1) ?? '')).toEqual({
        page: '1',
        pageSize: '25',
        sort: 'firstName',
        scope: 'Independent',
        id: 'user-1',
      }),
    )

    await click(
      findButton(t('pages.admin.users.dependentsLabel', { count: 2 }), rowElement(wrapper, 0)),
    )
    const dependentsOf = t('pages.admin.users.relation.dependentsOf', { fullName: 'Ada Lovelace' })
    expect(chipLabels(wrapper)).toEqual([dependentsOf])
    expect(selectedScope(wrapper)).toBe(t('pages.admin.users.scope.dependent'))
    await vi.waitFor(() =>
      expect(queryOf(recorded.list.at(-1) ?? '')).toMatchObject({
        scope: 'Dependent',
        parentId: 'user-1',
      }),
    )

    await click(findButton(t('table.removeFilter', { label: dependentsOf })))
    expect(wrapper.find('.filter-chip').exists()).toBe(false)
    await vi.waitFor(() =>
      expect(queryOf(recorded.list.at(-1) ?? '')).toEqual({
        page: '1',
        pageSize: '25',
        sort: 'firstName',
        scope: 'Dependent',
      }),
    )
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
    expect(selectFilter(wrapper, 'pages.admin.users.columns.staffRole').exists()).toBe(true)
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

  it('changes the cargo of another account with the password, and removes it without one', async () => {
    serveUsers([ada, grace])
    const requests: unknown[] = []
    server.use(
      http.patch('/api/users/:userId/staff-role', async ({ request, params }) => {
        requests.push({ id: params.userId, body: await request.json() })
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper } = await renderPage(SUPER_ADMIN)
    const staffRoleSelect = () => wrapper.findComponent(StaffRoleDialog).findComponent(ElSelect)

    await chooseRowAction(wrapper, 0, CHANGE_STAFF_ROLE)
    const dialog = openDialog(t(STAFF_ROLE_TITLE))
    expect(dialog.textContent).toContain(
      t('pages.admin.users.staffRole.message', { fullName: 'Ada Lovelace' }),
    )
    expect(staffRoleSelect().props('modelValue')).toBe('None')
    expect(dialog.querySelector('#staff-role-password')).toBeNull()
    staffRoleSelect().vm.$emit('update:modelValue', 'Editor')
    await flushPromises()

    await click(findButton(t('pages.admin.users.staffRole.confirm'), dialog))
    expect(dialog.textContent).toContain(
      t('pages.admin.users.adminPassword.form.problems.passwordRequired'),
    )
    expect(requests).toEqual([])

    await typeInto('#staff-role-password', 'Str0ngPass!23')
    await click(findButton(t('pages.admin.users.staffRole.confirm'), dialog))
    await expectNotification(t('pages.admin.users.toasts.staffRoleChanged'))
    await vi.waitFor(() => expect(isDialogOpen(t(STAFF_ROLE_TITLE))).toBe(false))

    await chooseRowAction(wrapper, 1, CHANGE_STAFF_ROLE)
    const again = openDialog(t(STAFF_ROLE_TITLE))
    expect(staffRoleSelect().props('modelValue')).toBe('Administrator')
    staffRoleSelect().vm.$emit('update:modelValue', 'None')
    await flushPromises()
    expect(again.querySelector('#staff-role-password')).toBeNull()
    await click(findButton(t('pages.admin.users.staffRole.confirm'), again))

    await vi.waitFor(() => expect(requests).toHaveLength(2))
    expect(requests).toEqual([
      { id: 'user-1', body: { staffRole: 'Editor', currentPassword: 'Str0ngPass!23' } },
      { id: 'user-3', body: { staffRole: 'None', currentPassword: null } },
    ])
  })

  it('keeps the cargo dialog open when the password is rejected or the request fails', async () => {
    serveUsers([ada])
    let rejectPassword = true
    server.use(
      http.patch('/api/users/:userId/staff-role', () =>
        rejectPassword ? apiError(400, 'UserCurrentPasswordIncorrect') : apiError(500),
      ),
    )
    const { wrapper } = await renderPage(SUPER_ADMIN)
    const staffRoleSelect = () => wrapper.findComponent(StaffRoleDialog).findComponent(ElSelect)

    await chooseRowAction(wrapper, 0, CHANGE_STAFF_ROLE)
    const dialog = openDialog(t(STAFF_ROLE_TITLE))
    staffRoleSelect().vm.$emit('update:modelValue', 'Administrator')
    await flushPromises()
    await typeInto('#staff-role-password', 'wrong-password')
    await click(findButton(t('pages.admin.users.staffRole.confirm'), dialog))

    await vi.waitFor(() =>
      expect(dialog.textContent).toContain(t('errors.UserCurrentPasswordIncorrect')),
    )
    expect(document.body.querySelectorAll('.el-notification')).toHaveLength(0)
    expect(isDialogOpen(t(STAFF_ROLE_TITLE))).toBe(true)

    rejectPassword = false
    await click(findButton(t('pages.admin.users.staffRole.confirm'), dialog))
    await expectNotification(t('common.error'))
    expect(dialog.textContent).not.toContain(t('errors.UserCurrentPasswordIncorrect'))
    expect(isDialogOpen(t(STAFF_ROLE_TITLE))).toBe(true)

    await dismissDialog(wrapper, t(STAFF_ROLE_TITLE))
    await vi.waitFor(() => expect(isDialogOpen(t(STAFF_ROLE_TITLE))).toBe(false))
    await chooseRowAction(wrapper, 0, CHANGE_STAFF_ROLE)
    expect(staffRoleSelect().props('modelValue')).toBe('None')
    expect(document.querySelector('#staff-role-password')).toBeNull()
  })

  it('designates the data protection officer, reloading the session only when it changes', async () => {
    serveUsers([{ ...ada, staffRole: 'Administrator' }])
    const designated: unknown[] = []
    let meCalls = 0
    server.use(
      http.post('/api/users/:userId/privacy-officer', async ({ request, params }) => {
        designated.push({ id: params.userId, body: await request.json() })
        return new HttpResponse(null, { status: 204 })
      }),
      http.get('/api/auth/me', () => {
        meCalls += 1
        return HttpResponse.json(
          buildUserResponse({ id: 'admin-0', staffRole: 'Administrator', isInitialAdmin: true }),
        )
      }),
    )
    const { wrapper, queryClient } = await renderPage({ ...SUPER_ADMIN, isPrivacyOfficer: true })

    await chooseRowAction(wrapper, 0, DESIGNATE_OFFICER)
    const dialog = openDialog(t(OFFICER_TITLE))
    expect(dialog.textContent).toContain(
      t('pages.admin.users.privacyOfficer.message', { fullName: 'Ada Lovelace' }),
    )
    await click(findButton(t('pages.admin.users.privacyOfficer.confirm'), dialog))
    expect(dialog.textContent).toContain(
      t('pages.admin.users.adminPassword.form.problems.passwordRequired'),
    )
    expect(designated).toEqual([])

    await typeInto('#privacy-officer-password', 'Str0ngPass!23')
    await click(findButton(t('pages.admin.users.privacyOfficer.confirm'), dialog))

    await expectNotification(t('pages.admin.users.toasts.privacyOfficerDesignated'))
    expect(designated).toEqual([{ id: 'user-1', body: { currentPassword: 'Str0ngPass!23' } }])
    await vi.waitFor(() => expect(sessionOf(queryClient).isPrivacyOfficer).toBe(false))
    expect(meCalls).toBe(1)
    await vi.waitFor(() => expect(isDialogOpen(t(OFFICER_TITLE))).toBe(false))

    await chooseRowAction(wrapper, 0, DESIGNATE_OFFICER)
    await typeInto('#privacy-officer-password', 'Str0ngPass!23')
    await click(findButton(t('pages.admin.users.privacyOfficer.confirm'), dialog))
    await vi.waitFor(() => expect(designated).toHaveLength(2))
    await flushPromises()
    expect(meCalls).toBe(1)
  })

  it('shows the cargo, designation and deletion actions only to the superadministrator', async () => {
    serveUsers([
      buildUserResponse({
        id: 'admin-0',
        firstName: 'Super',
        lastName: 'Admin',
        staffRole: 'Administrator',
        isInitialAdmin: true,
        isPrivacyOfficer: true,
      }),
      grace,
      tim,
    ])
    const { wrapper } = await renderPage(SUPER_ADMIN)

    expect(rows(wrapper).map((row) => row.find('.staff-cell > span').text())).toEqual([
      t('pages.admin.users.staffRole.superAdmin'),
      t('entities.user.staffRole.Administrator'),
      '—',
    ])
    const ownMenu = await openRowMenu(rowElement(wrapper, 0))
    expect(rowMenuLabels(ownMenu)).toEqual([t(CHANGE_TYPE), t(SEND_EMAIL), t('common.delete')])
    const ownDelete = rowMenuItem(ownMenu, t('common.delete'))
    expect(ownDelete.getAttribute('aria-disabled')).toBe('true')
    expect(textOf(ownDelete.querySelector('.row-actions__item-reason'))).toBe(
      t('pages.admin.users.blocked.initialAdmin'),
    )
    await click(ownDelete)
    expect(document.body.querySelector('.el-message-box')).toBeNull()

    expect(rowMenuLabels(await openRowMenu(rowElement(wrapper, 1)))).toEqual([
      t(CHANGE_TYPE),
      t(SEND_EMAIL),
      t(RESET_2FA),
      t(CHANGE_STAFF_ROLE),
      t(DESIGNATE_OFFICER),
      t('common.delete'),
    ])
    expect(rowMenuLabels(await openRowMenu(rowElement(wrapper, 2)))).toEqual([
      t(CHANGE_TYPE),
      t('common.delete'),
    ])
    wrapper.unmount()

    const admin = await renderPage({ id: 'user-9', staffRole: 'Administrator' })
    expect(rowMenuLabels(await openRowMenu(rowElement(admin.wrapper, 1)))).toEqual([
      t(CHANGE_TYPE),
      t(SEND_EMAIL),
      t(RESET_2FA),
    ])
  })

  it('marks the own account and keeps its type and second-factor reset out of reach', async () => {
    const requests: string[] = []
    serveUsers([
      buildUserResponse({ staffRole: 'Administrator' }),
      buildUserResponse({ id: 'user-2', firstName: 'Grace', lastName: 'Hopper' }),
    ])
    server.use(
      http.patch('/api/users/:userId/change-type', ({ params }) => {
        requests.push(String(params.userId))
        return HttpResponse.json(buildUserResponse())
      }),
    )
    const { wrapper } = await renderPage({ id: 'user-1', staffRole: 'Administrator' })
    const [own, other] = rows(wrapper)

    expect(textOf(own?.find('.table-primary-cell__badges').element)).toContain(
      t('pages.admin.users.badges.you'),
    )
    expect(other?.find('.table-primary-cell__badges').exists()).toBe(false)

    const ownMenu = await openRowMenu(rowElement(wrapper, 0))
    expect(rowMenuLabels(ownMenu)).toEqual([t(CHANGE_TYPE), t(SEND_EMAIL)])
    const ownType = rowMenuItem(ownMenu, t(CHANGE_TYPE))
    expect(ownType.getAttribute('aria-disabled')).toBe('true')
    expect(textOf(ownType.querySelector('.row-actions__item-reason'))).toBe(
      t('pages.admin.users.blocked.selfRole'),
    )
    await click(ownType)
    await flushPromises()
    expect(isDialogOpen(t(TYPE_TITLE))).toBe(false)
    expect(requests).toEqual([])

    const otherMenu = await openRowMenu(rowElement(wrapper, 1))
    expect(rowMenuLabels(otherMenu)).toEqual([t(CHANGE_TYPE), t(SEND_EMAIL), t(RESET_2FA)])
    const otherType = rowMenuItem(otherMenu, t(CHANGE_TYPE))
    expect(otherType.getAttribute('aria-disabled')).not.toBe('true')
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
    const { wrapper } = await renderPage(SUPER_ADMIN)

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
    await click(dialog.querySelector('input[type="radio"][value="News"]')!)
    expect(dialog.querySelector('.el-alert--info')?.textContent).toContain(
      tp('features.sendEmail.withoutConsent', 1, { count: 1 }),
    )
    await typeInto('#send-email-subject', 'News')
    await typeInto('#send-email-body', 'Hello everyone')
    await click(findButton(t('features.sendEmail.send'), dialog))
    await acceptMessageBox()

    await expectNotification(tp('features.sendEmail.toast.queued', 2, { count: 2 }))
    expect(received?.subject).toBe('News')
    expect(received?.purpose).toBe('News')
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
    await click(dialog.querySelector('input[type="radio"][value="Notice"]')!)
    await typeInto('#send-email-subject', 'Hi')
    await typeInto('#send-email-body', 'Personal note')
    await click(findButton(t('features.sendEmail.send'), dialog))
    await acceptMessageBox()
    await expectNotification(tp('features.sendEmail.toast.queued', 1, { count: 1 }))
    expect(sentTo).toEqual(['user-1'])

    fail = true
    await chooseRowAction(wrapper, 0, SEND_EMAIL)
    dialog = openDialog(t(EMAIL_TITLE))
    await click(dialog.querySelector('input[type="radio"][value="Notice"]')!)
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
