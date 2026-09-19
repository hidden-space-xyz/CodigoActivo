import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { ElDialog, ElPagination, ElSelect } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { genderLabel } from '@/entities/user'
import SendEmailDialog from '@/features/send-email/ui/SendEmailDialog.vue'
import EventAttendeesTab from '@/pages/admin/event-detail/ui/EventAttendeesTab.vue'
import type { EventAttendeeResponse } from '@/shared/api/generated/models'
import { i18n } from '@/shared/i18n'
import { ageFrom } from '@/shared/lib'

import {
  bodyButtons,
  buildActivity,
  buildAssignment,
  buildAttendee,
  EVENT_ID,
  notificationsText,
  propOf,
  useCatalogHandlers,
} from '../../../../support/fixtures/admin-events/builders'
import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'

function tc(key: string, count: number): string {
  return (i18n.global.t as (key: string, plural: number) => string)(key, count)
}

const guardianAttendee = buildAttendee({
  userId: 'user-2',
  firstName: 'Tim',
  lastName: 'Berners',
  email: null,
  phone: null,
  birthDate: '',
  gender: undefined,
  userTypeName: null,
  userTypeColor: null,
  guardian: { firstName: 'Mary', lastName: 'Berners', email: null, phone: null },
  assignments: [
    buildAssignment({
      activityId: 'act-2',
      activityTitle: null,
      roleTypeName: null,
      statusId: undefined,
      statusName: null,
      hasTimeConflict: true,
    }),
  ],
})

interface RenderOptions {
  attendees?: EventAttendeeResponse[]
  total?: number
  urls?: URL[]
  props?: Record<string, unknown>
  catalog?: boolean
}

async function renderTab(options: RenderOptions = {}) {
  const attendees = options.attendees ?? [buildAttendee(), guardianAttendee]
  if (options.catalog !== false) useCatalogHandlers()
  server.use(
    http.get('/api/reports/events/:eventId/attendees', ({ request }) => {
      const url = new URL(request.url)
      options.urls?.push(url)
      return HttpResponse.json(paged(attendees, options.total ?? attendees.length))
    }),
  )
  const rendered = await renderWithProviders(EventAttendeesTab, {
    props: {
      eventId: EVENT_ID,
      active: true,
      activities: [buildActivity(), buildActivity({ id: undefined, title: null })],
      activitiesLoading: false,
      activitiesError: false,
      ...options.props,
    },
    attach: true,
  })
  await flushPromises()
  return rendered
}

function selectByPlaceholder(wrapper: VueWrapper, placeholder: string) {
  const select = wrapper
    .findAllComponents(ElSelect)
    .find((candidate) => propOf(candidate, 'placeholder') === placeholder)
  if (!select) throw new Error(`Select not found: ${placeholder}`)
  return select
}

function selectById(wrapper: VueWrapper, id: string) {
  const select = wrapper
    .findAllComponents(ElSelect)
    .find((candidate) => candidate.props('id') === id)
  if (!select) throw new Error(`Select not found: ${id}`)
  return select
}

function button(label: string, index = 0): HTMLButtonElement {
  const found = bodyButtons(label)[index]
  if (!found) throw new Error(`Button not found: ${label}`)
  return found
}

function dialogByTitle(title: string): Element {
  const dialog = [...document.body.querySelectorAll('.el-dialog')].find(
    (node) => node.querySelector('.el-dialog__title')?.textContent === title,
  )
  if (!dialog) throw new Error(`Dialog not found: ${title}`)
  return dialog
}

describe('EventAttendeesTab', () => {
  it('lists attendees with identity, contact, type and assignment details', async () => {
    const { wrapper } = await renderTab()

    await vi.waitFor(() => expect(wrapper.text()).toContain('Ada Lovelace'))
    const text = wrapper.text()
    expect(text).toContain(tc('pages.admin.eventDetail.attendees.count', 2))
    expect(text).toContain(
      t('pages.admin.eventDetail.attendees.age', { age: ageFrom('1990-01-15') }),
    )
    expect(text).toContain('ada@example.test')
    expect(text).toContain('600000001')
    expect(text).toContain(
      t('pages.admin.eventDetail.attendees.guardian', { firstName: 'Mary', lastName: 'Berners' }),
    )
    expect(text).toContain(t('pages.admin.eventDetail.attendees.conflict.badge'))

    const [ada, tim] = wrapper.findAll('li.attendee')
    expect(ada?.attributes('style')).toContain('--user-type: #ff0000')
    expect(tim?.attributes('style')).toBeUndefined()
    expect(ada?.find('.attendee__type').text()).toBe('Member')
    expect(tim?.find('.attendee__type').text()).toBe('—')
    expect(tim?.find('.attendee__age').exists()).toBe(false)
    expect(ada?.find('.assignment__title').text()).toBe('Robotics')
    expect(ada?.find('.assignment__role').text()).toBe('Volunteer')
    expect(ada?.find('.assignment__status').text()).toContain('Requested')
    expect(tim?.find('.assignment__title').text()).toBe('—')
    expect(tim?.find('.assignment__role').text()).toBe('—')
    expect(tim?.find('.assignment__warning').exists()).toBe(true)
    expect(bodyButtons(t('pages.admin.eventDetail.attendees.email.rowLabel'))).toHaveLength(1)
    expect(wrapper.findComponent(ElPagination).exists()).toBe(false)
  })

  it('shows the parent loading and error states', async () => {
    const loading = await renderTab({ props: { activitiesLoading: true } })
    expect(loading.wrapper.text()).toContain(t('common.loading'))
    loading.wrapper.unmount()

    const failing = await renderTab({ props: { activitiesError: true } })
    expect(failing.wrapper.text()).toContain(t('dataState.error'))
  })

  it('distinguishes an empty event from filters without matches', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderTab({ attendees: [], urls })

    await vi.waitFor(() =>
      expect(wrapper.text()).toContain(t('pages.admin.eventDetail.attendees.empty.none')),
    )
    expect(button(t('pages.admin.eventDetail.attendees.export.label')).disabled).toBe(true)

    await wrapper.find('.toolbar__search input').setValue('zzz')
    expect(wrapper.text()).toContain(t('pages.admin.eventDetail.attendees.empty.noMatches'))
  })

  it('debounces the search and sends the filters to the API', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderTab({ urls })
    const search = wrapper.find('.toolbar__search input')

    await search.setValue('ad')
    await search.setValue('ada')
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('search')).toBe('ada'))
    expect(urls.filter((url) => url.searchParams.has('search'))).toHaveLength(1)

    const type = selectByPlaceholder(wrapper, t('pages.admin.eventDetail.attendees.type'))
    expect(type.findAllComponents({ name: 'ElOption' })).toHaveLength(2)
    type.vm.$emit('update:modelValue', 'type-1')
    selectByPlaceholder(wrapper, t('common.gender')).vm.$emit('update:modelValue', 'Female')
    selectByPlaceholder(wrapper, t('pages.admin.eventDetail.attendees.filters.activity')).vm.$emit(
      'update:modelValue',
      'act-1',
    )
    selectByPlaceholder(wrapper, t('pages.admin.eventDetail.attendees.role')).vm.$emit(
      'update:modelValue',
      'role-2',
    )
    selectByPlaceholder(wrapper, t('common.status')).vm.$emit('update:modelValue', 'status-2')

    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('statusId')).toBe('status-2'))
    const last = urls.at(-1)
    expect(last?.searchParams.get('userTypeId')).toBe('type-1')
    expect(last?.searchParams.get('gender')).toBe('Female')
    expect(last?.searchParams.get('activityId')).toBe('act-1')
    expect(last?.searchParams.get('roleTypeId')).toBe('role-2')

    type.vm.$emit('update:modelValue', '')
    selectByPlaceholder(wrapper, t('common.status')).vm.$emit('update:modelValue', undefined)
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.has('userTypeId')).toBe(false))
    expect(urls.at(-1)?.searchParams.has('statusId')).toBe(false)
    expect(type.props('modelValue')).toBeNull()
  })

  it('cancels a pending search when unmounted', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderTab({ urls })
    const before = urls.length

    await wrapper.find('.toolbar__search input').setValue('ada')
    wrapper.unmount()
    await new Promise((resolve) => setTimeout(resolve, 350))

    expect(urls).toHaveLength(before)
  })

  it('changes the sort field and direction', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderTab({ urls })
    const sort = wrapper
      .findAllComponents(ElSelect)
      .find(
        (select) =>
          select.props('ariaLabel') === t('pages.admin.eventDetail.attendees.sort.ariaSortBy'),
      )
    expect(sort?.props('modelValue')).toBe('firstName')

    sort?.vm.$emit('update:modelValue', 'type,firstName')
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('sort')).toBe('type,firstName'))

    button(t('pages.admin.eventDetail.attendees.sort.ascending')).click()
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('sort')).toBe('-type,firstName'))

    button(t('pages.admin.eventDetail.attendees.sort.descending')).click()
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('sort')).toBe('type,firstName'))
  })

  it('pages through large attendee lists', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderTab({ total: 60, urls })

    const pagination = wrapper.findComponent(ElPagination)
    expect(pagination.exists()).toBe(true)
    pagination.vm.$emit('update:current-page', 3)
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('page')).toBe('3'))
    pagination.vm.$emit('update:page-size', 50)
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('pageSize')).toBe('50'))
  })

  it('exports the filtered attendees as CSV', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderTab({ urls })
    let csv: Blob | undefined
    vi.spyOn(URL, 'createObjectURL').mockImplementation((blob) => {
      csv = blob as Blob
      return 'blob:csv'
    })
    const downloads: string[] = []
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      downloads.push(this.download)
    })
    selectByPlaceholder(wrapper, t('common.gender')).vm.$emit('update:modelValue', 'Male')
    await flushPromises()

    button(t('pages.admin.eventDetail.attendees.export.label')).click()

    await vi.waitFor(() =>
      expect(notificationsText()).toContain(
        tc('pages.admin.eventDetail.attendees.export.toast.exported', 2),
      ),
    )
    expect(downloads).toHaveLength(1)
    expect(downloads[0]).toMatch(/^asistentes-\d{4}-\d{2}-\d{2}\.csv$/)
    const exportUrl = urls.find((url) => url.searchParams.get('pageSize') === '100')
    expect(exportUrl?.searchParams.get('gender')).toBe('Male')
    const content = (await csv?.text()) ?? ''
    const lines = content.replace('﻿', '').split('\r\n')
    expect(lines[0]).toBe(
      'Nombre;Apellidos;Email;Numero;Genero;NombreDelTutor;ApellidosDelTutor;EmailDelTutor;NumeroDelTutor',
    )
    expect(lines[1]).toBe(`Ada;Lovelace;ada@example.test;600000001;${genderLabel('Female')};;;;`)
    expect(lines[2]).toBe('Tim;Berners;;;;Mary;Berners;;')
  })

  it('reports export failures', async () => {
    const { wrapper } = await renderTab()
    server.use(http.get('/api/reports/events/:eventId/attendees', () => apiError(500)))
    await flushPromises()
    expect(wrapper.text()).toContain('Ada Lovelace')

    button(t('pages.admin.eventDetail.attendees.export.label')).click()

    await vi.waitFor(() => expect(notificationsText()).toContain(t('errors.generic')))
  })

  it('queues email for the filtered attendees and for a single attendee', async () => {
    const sent: { path: string; query: Record<string, string> }[] = []
    server.use(
      http.post('/api/emails/events/:eventId/attendees', ({ request }) => {
        const url = new URL(request.url)
        sent.push({ path: url.pathname, query: Object.fromEntries(url.searchParams) })
        return HttpResponse.json({ queued: 2, skipped: 0 })
      }),
      http.post('/api/emails/users/:userId', ({ request }) => {
        const url = new URL(request.url)
        sent.push({ path: url.pathname, query: Object.fromEntries(url.searchParams) })
        return HttpResponse.json({ queued: 1 })
      }),
    )
    const { wrapper } = await renderTab()
    const dialog = wrapper.findComponent(SendEmailDialog)
    const payload = { subject: 'Hello', body: 'World', attachments: [] }

    selectByPlaceholder(wrapper, t('common.status')).vm.$emit('update:modelValue', 'status-1')
    await flushPromises()
    button(t('pages.admin.eventDetail.attendees.email.bulkLabel')).click()
    await flushPromises()
    expect(dialog.props('visible')).toBe(true)
    expect(dialog.props('target')).toBe(
      tc('pages.admin.eventDetail.attendees.email.targetFiltered', 2),
    )
    dialog.vm.$emit('submit', payload)
    await vi.waitFor(() => expect(dialog.props('visible')).toBe(false))

    button(t('pages.admin.eventDetail.attendees.email.rowLabel')).click()
    await flushPromises()
    expect(dialog.props('target')).toBe('Ada Lovelace')
    dialog.vm.$emit('submit', payload)
    await vi.waitFor(() => expect(sent).toHaveLength(2))

    expect(sent[0]).toEqual({
      path: `/api/emails/events/${EVENT_ID}/attendees`,
      query: { statusId: 'status-1' },
    })
    expect(sent[1]?.path).toBe('/api/emails/users/user-1')
  })

  it('reports email failures', async () => {
    server.use(http.post('/api/emails/events/:eventId/attendees', () => apiError(500)))
    const { wrapper } = await renderTab()
    const dialog = wrapper.findComponent(SendEmailDialog)

    button(t('pages.admin.eventDetail.attendees.email.bulkLabel')).click()
    await flushPromises()
    dialog.vm.$emit('submit', { subject: 'Hello', body: 'World', attachments: [] })

    await vi.waitFor(() => expect(notificationsText()).toContain(t('errors.generic')))
    expect(dialog.props('visible')).toBe(true)
  })

  it('changes an assignment role', async () => {
    const changes: { path: string; body: unknown }[] = []
    server.use(
      http.patch('/api/activities/:activityId/:userId/change-role', async ({ request }) => {
        changes.push({ path: new URL(request.url).pathname, body: await request.json() })
        return changes.length === 1 ? HttpResponse.json({}) : apiError(500)
      }),
    )
    const { wrapper } = await renderTab()

    button(t('pages.admin.eventDetail.attendees.changeRole')).click()
    await flushPromises()
    const dialog = dialogByTitle(t('pages.admin.eventDetail.attendees.changeRole'))
    expect(dialog.textContent).toContain('Ada Lovelace · Robotics')
    const roleSelect = selectById(wrapper, 'attendee-role')
    expect(roleSelect.props('modelValue')).toBe('role-1')
    roleSelect.vm.$emit('update:modelValue', 'role-2')
    await flushPromises()

    button(t('common.apply'), 0).click()
    await vi.waitFor(() =>
      expect(notificationsText()).toContain(
        t('pages.admin.eventDetail.attendees.toast.roleUpdated'),
      ),
    )
    expect(changes).toEqual([
      { path: '/api/activities/act-1/user-1/change-role', body: { activityRoleTypeId: 'role-2' } },
    ])

    button(t('pages.admin.eventDetail.attendees.changeRole')).click()
    await flushPromises()
    const apply = bodyButtons(t('common.apply'), dialog)[0]
    apply?.click()
    await vi.waitFor(() => expect(notificationsText()).toContain(t('errors.generic')))

    bodyButtons(t('common.cancel'), dialog)[0]?.click()
    await flushPromises()
    expect(changes).toHaveLength(2)
  })

  it('warns when roles are unavailable and skips assignments without identifiers', async () => {
    let patches = 0
    server.use(
      http.get('/api/activities/roleType', () => HttpResponse.json([])),
      http.get('/api/activities/assignment-status-types', () => HttpResponse.json([])),
      http.get('/api/users/types', () => HttpResponse.json([])),
      http.patch('/api/activities/:activityId/:userId/:action', () => {
        patches += 1
        return HttpResponse.json({})
      }),
    )
    const { wrapper } = await renderTab({
      catalog: false,
      attendees: [buildAttendee({ userId: undefined, email: null })],
    })

    button(t('pages.admin.eventDetail.attendees.changeRole')).click()
    await flushPromises()
    const roleDialog = dialogByTitle(t('pages.admin.eventDetail.attendees.changeRole'))
    expect(roleDialog.textContent).toContain(t('pages.admin.eventDetail.attendees.rolesLoadError'))
    expect(bodyButtons(t('common.apply'), roleDialog)[0]?.disabled).toBe(true)

    button(t('pages.admin.eventDetail.attendees.changeStatus')).click()
    await flushPromises()
    const statusDialog = dialogByTitle(t('pages.admin.eventDetail.attendees.changeStatus'))
    bodyButtons(t('common.apply'), statusDialog)[0]?.click()
    await flushPromises()

    const statusSelect = selectById(wrapper, 'attendee-status')
    const roleSelect = selectById(wrapper, 'attendee-role')
    roleSelect.vm.$emit('update:modelValue', 'role-1')
    await flushPromises()
    bodyButtons(t('common.apply'), roleDialog)[0]?.click()
    await flushPromises()

    expect(statusSelect.props('modelValue')).toBe('status-1')
    expect(patches).toBe(0)
  })

  it('closes the email, role and status dialogs when dismissed', async () => {
    const { wrapper } = await renderTab()
    const email = wrapper.findComponent(SendEmailDialog)

    button(t('pages.admin.eventDetail.attendees.email.bulkLabel')).click()
    await flushPromises()
    email.vm.$emit('update:visible', false)
    await flushPromises()
    expect(email.props('visible')).toBe(false)

    button(t('pages.admin.eventDetail.attendees.changeRole')).click()
    button(t('pages.admin.eventDetail.attendees.changeStatus')).click()
    await flushPromises()
    const dialogs = wrapper
      .findAllComponents(ElDialog)
      .filter((dialog) => dialog.props('modelValue') === true)
    expect(dialogs).toHaveLength(2)
    for (const dialog of dialogs) dialog.vm.$emit('update:modelValue', false)
    await flushPromises()

    expect(
      wrapper.findAllComponents(ElDialog).filter((dialog) => dialog.props('modelValue') === true),
    ).toHaveLength(0)
  })

  it('changes an assignment status', async () => {
    const changes: { path: string; body: unknown }[] = []
    server.use(
      http.patch('/api/activities/:activityId/:userId/change-status', async ({ request }) => {
        changes.push({ path: new URL(request.url).pathname, body: await request.json() })
        return changes.length === 1 ? HttpResponse.json({}) : apiError(500)
      }),
    )
    const { wrapper } = await renderTab({ attendees: [guardianAttendee] })

    button(t('pages.admin.eventDetail.attendees.changeStatus')).click()
    await flushPromises()
    const dialog = dialogByTitle(t('pages.admin.eventDetail.attendees.changeStatus'))
    expect(dialog.textContent).toContain('Tim Berners')
    const statusSelect = selectById(wrapper, 'attendee-status')
    expect(statusSelect.props('modelValue')).toBeNull()
    expect(bodyButtons(t('common.apply'), dialog)[0]?.disabled).toBe(true)

    statusSelect.vm.$emit('update:modelValue', 'status-2')
    await flushPromises()
    bodyButtons(t('common.apply'), dialog)[0]?.click()
    await vi.waitFor(() =>
      expect(notificationsText()).toContain(
        t('pages.admin.eventDetail.attendees.toast.statusUpdated'),
      ),
    )
    expect(changes).toEqual([
      {
        path: '/api/activities/act-2/user-2/change-status',
        body: { assignmentStatusId: 'status-2' },
      },
    ])

    button(t('pages.admin.eventDetail.attendees.changeStatus')).click()
    await flushPromises()
    statusSelect.vm.$emit('update:modelValue', 'status-1')
    await flushPromises()
    bodyButtons(t('common.apply'), dialog)[0]?.click()
    await vi.waitFor(() => expect(notificationsText()).toContain(t('errors.generic')))

    bodyButtons(t('common.cancel'), dialog)[0]?.click()
    await flushPromises()
    expect(changes).toHaveLength(2)
  })
})
