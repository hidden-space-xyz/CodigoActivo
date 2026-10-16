import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { ElPagination, ElTable } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import type { EventInput } from '@/entities/event'
import { EventsAdminPage } from '@/pages/admin/events'
import EventFormDialog from '@/pages/admin/events/ui/EventFormDialog.vue'
import type {
  CreateEventRequest,
  EventListItemResponse,
  EventResponse,
} from '@/shared/api/generated/models'

import { renderWithProviders, t } from '../../../../../support/render'
import { apiError, http, HttpResponse, paged, server } from '../../../../../support/server'
import { useCatalogHandlers } from '../../../../../support/api/catalogs'
import {
  buildEventCategory,
  buildEventListItem,
  buildEventResponse,
  buildEventTermsLink,
  EVENT_ID,
  THUMBNAIL_ID,
} from '../../../../../support/builders'
import {
  click,
  findButtons,
  notificationTexts,
  openRowMenu,
  queryOf,
  rowMenuItem,
  rowMenuLabels,
  textOf,
} from '../../../../../support/dom'
import { ColumnFilterDate, ColumnFilterSelect, ColumnSearch } from '@/shared/ui/column-filter'
import { SearchInput } from '@/shared/ui/search-input'
import { formatDateRange, formatDateTime, formatDateTimeRange } from '@/shared/lib/date'

const ADMIN_EVENT = {
  title: 'Hackathon',
  subtitle: 'Code all night',
  eventStartsAt: '2026-10-10',
  eventEndsAt: '2026-10-12',
  signupStartsAt: '2026-09-01T08:00:00.000Z',
  signupEndsAt: '2026-10-01T20:00:00.000Z',
  categories: [buildEventCategory({ name: 'Tech', color: '#112233' })],
}

function buildAdminEventListItem(
  overrides: Partial<EventListItemResponse> = {},
): EventListItemResponse {
  return buildEventListItem({ ...ADMIN_EVENT, ...overrides })
}

function buildAdminEvent(overrides: Partial<EventResponse> = {}): EventResponse {
  return buildEventResponse({
    ...ADMIN_EVENT,
    description: '{"type":"doc","content":[]}',
    termsDocuments: [buildEventTermsLink()],
    ...overrides,
  })
}

const input: EventInput = {
  title: 'Hackathon',
  subtitle: 'Code all night',
  description: '{}',
  categoryIds: ['cat-1'],
  startsAt: '2026-10-10',
  endsAt: '2026-10-12',
  earlySignupStartsAt: null,
  signupStartsAt: '2026-09-01T08:00:00.000Z',
  signupEndsAt: '2026-10-01T20:00:00.000Z',
  thumbnailId: THUMBNAIL_ID,
  terms: [],
}

const request: CreateEventRequest = {
  title: 'Hackathon',
  subtitle: 'Code all night',
  description: '{}',
  categoryTypeIds: ['cat-1'],
  eventStartsAt: '2026-10-10',
  eventEndsAt: '2026-10-12',
  earlySignupStartsAt: null,
  signupStartsAt: '2026-09-01T08:00:00.000Z',
  signupEndsAt: '2026-10-01T20:00:00.000Z',
  thumbnailId: THUMBNAIL_ID,
  termsDocuments: null,
}

function listHandler(items: EventListItemResponse[], urls: URL[] = []) {
  return http.get('/api/events', ({ request: req }) => {
    urls.push(new URL(req.url))
    return HttpResponse.json(paged(items, items.length))
  })
}

async function renderPage(
  items: EventListItemResponse[] = [buildAdminEventListItem()],
  urls: URL[] = [],
) {
  useCatalogHandlers()
  server.use(listHandler(items, urls))
  const rendered = await renderWithProviders(EventsAdminPage, {
    route: '/admin/events',
    attach: true,
  })
  await vi.waitFor(() => expect(urls.length + items.length).toBeGreaterThan(0))
  await flushPromises()
  return rendered
}

function rowButton(label: string, index = 0): HTMLButtonElement {
  const button = findButtons(label)[index]
  if (!button) throw new Error(`Button not found: ${label}`)
  return button
}

function tableRow(index = 0): HTMLElement {
  const row = document.body.querySelectorAll<HTMLElement>('.el-table__body tr.el-table__row')[index]
  if (!row) throw new Error(`Table row ${String(index)} not found`)
  return row
}

async function chooseRowMenuItem(label: string, index = 0): Promise<void> {
  const menu = await openRowMenu(tableRow(index))
  await click(rowMenuItem(menu, label))
}

async function chooseScope(wrapper: VueWrapper, label: string): Promise<void> {
  const item = wrapper
    .findAll('.el-segmented__item')
    .find((candidate) => candidate.text() === label)
  if (!item) throw new Error(`Scope "${label}" not found`)
  await click(item.find('input').element)
}

function headerCell(wrapper: VueWrapper, label: string) {
  const cell = wrapper
    .findAll('.el-table__header th')
    .find((candidate) => candidate.text().includes(label))
  if (!cell) throw new Error(`Header "${label}" not found`)
  return cell
}

async function confirmMessageBox(previousBoxes = 0) {
  await vi.waitFor(() =>
    expect(document.body.querySelectorAll('.el-message-box').length).toBeGreaterThan(previousBoxes),
  )
  const box = [...document.body.querySelectorAll('.el-message-box')].at(-1)
  if (!box) throw new Error('Message box not found')
  const confirm = findButtons(t('common.delete'), box)[0]
  if (!confirm) throw new Error('Confirm button not found')
  confirm.click()
  await flushPromises()
}

describe('EventsAdminPage', () => {
  it('lists events with their stage, categories, dates and early signup', async () => {
    const { wrapper } = await renderPage([
      buildAdminEventListItem({
        featured: true,
        earlySignupStartsAt: '2026-08-20T08:00:00Z',
        stage: 'EarlySignupOpen',
      }),
      buildAdminEventListItem({
        id: 'ev-2',
        title: 'Workshop',
        categories: [],
        eventStartsAt: '2026-11-07',
        eventEndsAt: '2026-11-07',
        stage: 'Finished',
      }),
      buildAdminEventListItem({
        id: 'ev-3',
        title: 'Festival',
        categories: [
          buildEventCategory({ categoryTypeId: 'cat-1', name: 'Tech', color: '#112233' }),
          buildEventCategory({ categoryTypeId: 'cat-2', name: 'Art', color: 'not-a-color' }),
          buildEventCategory({ categoryTypeId: 'cat-3', name: 'Music', color: '#778899' }),
        ],
      }),
    ])

    await vi.waitFor(() => expect(wrapper.text()).toContain('Workshop'))
    const text = wrapper.text()
    expect(text).toContain(t('pages.admin.events.header.title'))
    expect(text).toContain(formatDateRange('2026-10-10', '2026-10-12'))
    expect(text).toContain(formatDateRange('2026-11-07', '2026-11-07'))
    expect(formatDateRange('2026-11-07', '2026-11-07')).not.toContain('–')

    const [first, second, third] = [tableRow(0), tableRow(1), tableRow(2)]
    const title = first.querySelector('a.table-primary-cell__title')
    expect(title?.getAttribute('href')).toBe(`/admin/events/${EVENT_ID}`)
    expect(textOf(title)).toBe('Hackathon')
    expect(wrapper.find(`a[href="/admin/events/${EVENT_ID}"]`).exists()).toBe(true)
    expect(textOf(first.querySelector('.table-primary-cell__subtitle'))).toBe('Code all night')
    expect(first.querySelector('.table-primary-cell__leading .list-thumb--table')).not.toBeNull()
    const featured = first.querySelector('.table-primary-cell__badges .el-tag')
    expect(textOf(featured)).toBe(t('pages.admin.events.tag.featured'))
    expect(featured?.classList).toContain('el-tag--warning')
    expect(second.querySelector('.table-primary-cell__badges')).toBeNull()

    const firstCategories = [...first.querySelectorAll('.cats-cell .color-tag--dot')]
    expect(firstCategories.map(textOf)).toEqual(['Tech'])
    expect(textOf(second.querySelector('.cats-cell'))).toBe('—')
    const thirdCategories = [...third.querySelectorAll('.cats-cell .color-tag--dot')]
    expect(thirdCategories.map(textOf)).toEqual(['Tech', 'Art'])
    const more = third.querySelector('.cats-cell__more')
    expect(textOf(more)).toBe('+1')
    expect(more?.getAttribute('title')).toBe('Music')
    expect(first.querySelector('.cats-cell__more')).toBeNull()

    const firstSignup = first.querySelector('.signup-cell')
    const firstStage = firstSignup?.querySelector('.el-tag')
    expect(textOf(firstStage)).toBe(t('entities.event.status.earlySignupOpen'))
    expect(firstStage?.classList).toContain('el-tag--warning')
    expect(firstSignup?.querySelector('.signup-cell__dates')?.textContent).toBe(
      formatDateRange(ADMIN_EVENT.signupStartsAt, ADMIN_EVENT.signupEndsAt),
    )
    const signupWindow = formatDateTimeRange(ADMIN_EVENT.signupStartsAt, ADMIN_EVENT.signupEndsAt)
    expect(firstSignup?.getAttribute('title')).toBe(
      `${signupWindow} · ${t('pages.admin.events.earlySignupFrom', {
        date: formatDateTime('2026-08-20T08:00:00Z'),
      })}`,
    )
    const secondSignup = second.querySelector('.signup-cell')
    expect(secondSignup?.getAttribute('title')).toBe(signupWindow)
    expect(secondSignup?.querySelector('.el-tag')?.classList).toContain('el-tag--primary')
    expect(textOf(secondSignup?.querySelector('.el-tag'))).toBe(t('entities.event.status.finished'))
    expect(third.querySelector('.signup-cell .el-tag')?.classList).toContain('el-tag--success')
  })

  it('offers edit in the row and the other actions in a menu named after the event', async () => {
    await renderPage([
      buildAdminEventListItem({ featured: true }),
      buildAdminEventListItem({ id: 'ev-2', title: 'Workshop' }),
    ])

    const [first, second] = [tableRow(0), tableRow(1)]
    expect(findButtons(t('common.edit'), first)).toHaveLength(1)
    expect(findButtons(t('common.delete'), first)).toHaveLength(0)
    expect(first.querySelector('.row-actions__more')?.getAttribute('aria-label')).toBe(
      t('table.moreActionsFor', { name: 'Hackathon' }),
    )

    const featuredMenu = await openRowMenu(first)
    expect(rowMenuLabels(featuredMenu)).toEqual([
      t('pages.admin.events.actions.feature'),
      t('common.delete'),
    ])
    const featuredItem = rowMenuItem(featuredMenu, t('pages.admin.events.actions.feature'))
    expect(featuredItem.getAttribute('aria-disabled')).toBe('true')
    expect(textOf(featuredItem.querySelector('.row-actions__item-reason'))).toBe(
      t('pages.admin.events.alreadyFeatured'),
    )
    expect(rowMenuItem(featuredMenu, t('common.delete')).classList).toContain(
      'row-actions__item--danger',
    )

    const otherMenu = await openRowMenu(second)
    const otherItem = rowMenuItem(otherMenu, t('pages.admin.events.actions.feature'))
    expect(otherItem.getAttribute('aria-disabled')).not.toBe('true')
    expect(otherItem.querySelector('.row-actions__item-reason')).toBeNull()
  })

  it('shows the empty message of each scope and the error message', async () => {
    const { wrapper } = await renderPage([])
    await vi.waitFor(() =>
      expect(wrapper.text()).toContain(t('pages.admin.events.empty.noUpcoming')),
    )

    await chooseScope(wrapper, t('pages.admin.events.scope.past'))
    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.admin.events.empty.noPast')))

    await chooseScope(wrapper, t('pages.admin.events.scope.all'))
    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.admin.events.empty.none')))

    server.use(http.get('/api/events', () => apiError(500)))
    const failing = await renderWithProviders(EventsAdminPage, { route: '/admin/events' })
    await vi.waitFor(() =>
      expect(failing.wrapper.text()).toContain(t('pages.admin.events.empty.error')),
    )
  })

  it('sends sort and pagination changes to the API', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderPage([buildAdminEventListItem()], urls)

    wrapper.findComponent(ElTable).vm.$emit('sort-change', { prop: 'title', order: 'descending' })
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('sort')).toBe('-title'))

    const pagination = wrapper.findComponent(ElPagination)
    pagination.vm.$emit('size-change', 50)
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('pageSize')).toBe('50'))
    pagination.vm.$emit('current-change', 3)
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('page')).toBe('3'))
  })

  it('switches between upcoming, past and all events, newest first for the latter', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderPage([buildAdminEventListItem()], urls)

    expect(queryOf(urls[0]?.href ?? '')).toEqual({
      page: '1',
      pageSize: '25',
      sort: 'eventStartsAt',
      scope: 'Upcoming',
    })
    const segmented = wrapper.find('.el-segmented')
    expect(segmented.attributes('aria-label')).toBe(t('pages.admin.events.scope.label'))
    expect(segmented.findAll('.el-segmented__item').map((item) => item.text())).toEqual([
      t('pages.admin.events.scope.upcoming'),
      t('pages.admin.events.scope.past'),
      t('pages.admin.events.scope.all'),
    ])
    expect(segmented.find('.el-segmented__item.is-selected').text()).toBe(
      t('pages.admin.events.scope.upcoming'),
    )
    expect(headerCell(wrapper, t('pages.admin.events.columns.dates')).classes()).toContain(
      'ascending',
    )

    await chooseScope(wrapper, t('pages.admin.events.scope.past'))
    await vi.waitFor(() =>
      expect(queryOf(urls.at(-1)?.href ?? '')).toEqual({
        page: '1',
        pageSize: '25',
        sort: '-eventStartsAt',
        scope: 'Past',
      }),
    )
    await vi.waitFor(() =>
      expect(headerCell(wrapper, t('pages.admin.events.columns.dates')).classes()).toContain(
        'descending',
      ),
    )
    expect(segmented.find('.el-segmented__item.is-selected').text()).toBe(
      t('pages.admin.events.scope.past'),
    )

    await chooseScope(wrapper, t('pages.admin.events.scope.all'))
    await vi.waitFor(() =>
      expect(queryOf(urls.at(-1)?.href ?? '')).toEqual({
        page: '1',
        pageSize: '25',
        sort: '-eventStartsAt',
      }),
    )
    expect(wrapper.find('.data-table__chips').exists()).toBe(false)

    await chooseScope(wrapper, t('pages.admin.events.scope.upcoming'))
    await vi.waitFor(() =>
      expect(queryOf(urls.at(-1)?.href ?? '')).toEqual({
        page: '1',
        pageSize: '25',
        sort: 'eventStartsAt',
        scope: 'Upcoming',
      }),
    )
    await vi.waitFor(() =>
      expect(headerCell(wrapper, t('pages.admin.events.columns.dates')).classes()).toContain(
        'ascending',
      ),
    )
  })

  it('applies the toolbar search and the header column filters to the request', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderPage([buildAdminEventListItem()], urls)

    expect(wrapper.findAllComponents(ColumnSearch)).toHaveLength(0)
    const search = wrapper.findComponent(SearchInput)
    expect(search.props('label')).toBe(t('pages.admin.events.columns.searchEvent'))
    const [eventDate, signup] = wrapper.findAllComponents(ColumnFilterDate)
    expect(eventDate?.props('label')).toBe(t('pages.admin.events.columns.dates'))
    expect(signup?.props('label')).toBe(t('pages.admin.events.columns.signup'))
    search.vm.$emit('update:modelValue', 'hack')
    const category = wrapper.findComponent(ColumnFilterSelect)
    category.vm.$emit('update:modelValue', 'cat-2')
    category.vm.$emit('apply')
    eventDate?.vm.$emit('update:modelValue', [new Date(2026, 9, 1), new Date(2026, 9, 31)])
    eventDate?.vm.$emit('apply')
    signup?.vm.$emit('update:modelValue', [new Date(2026, 8, 1), new Date(2026, 8, 30)])
    signup?.vm.$emit('apply')

    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('signupTo')).toBe('2026-09-30'))
    const last = urls.at(-1)
    expect(last?.searchParams.get('search')).toBe('hack')
    expect(last?.searchParams.get('categoryTypeId')).toBe('cat-2')
    expect(last?.searchParams.get('eventDateFrom')).toBe('2026-10-01')
    expect(last?.searchParams.get('eventDateTo')).toBe('2026-10-31')
    expect(last?.searchParams.get('signupFrom')).toBe('2026-09-01')
    expect(last?.searchParams.get('scope')).toBe('Upcoming')
    expect(category.props('options')).toEqual([
      { label: 'Tech', value: 'cat-1' },
      { label: 'Art', value: 'cat-2' },
    ])
    await vi.waitFor(() =>
      expect(
        wrapper.findAll('.filter-chip__label').map((chip) => chip.element.textContent),
      ).toEqual([
        t('table.filterChip', { label: t('pages.admin.events.columns.categories'), value: 'Art' }),
        t('table.filterChip', {
          label: t('pages.admin.events.columns.dates'),
          value: formatDateRange('2026-10-01', '2026-10-31'),
        }),
        t('table.filterChip', {
          label: t('pages.admin.events.columns.signup'),
          value: formatDateRange('2026-09-01', '2026-09-30'),
        }),
      ]),
    )
  })

  it('closes the dialog when it asks to be hidden', async () => {
    const { wrapper } = await renderPage()
    rowButton(t('pages.admin.events.newEvent')).click()
    await flushPromises()
    const dialog = wrapper.findComponent(EventFormDialog)
    expect(dialog.props('visible')).toBe(true)

    dialog.vm.$emit('update:visible', false)
    await flushPromises()

    expect(dialog.props('visible')).toBe(false)
  })

  it('features an event from the row menu and reports failures', async () => {
    let calls = 0
    server.use(
      http.patch('/api/events/:eventId/feature', () => {
        calls += 1
        return calls === 1 ? HttpResponse.json(buildAdminEvent({ featured: true })) : apiError(500)
      }),
    )
    await renderPage()

    await chooseRowMenuItem(t('pages.admin.events.actions.feature'))
    await vi.waitFor(() =>
      expect(notificationTexts().join('\n')).toContain(t('pages.admin.events.toasts.featured')),
    )

    await chooseRowMenuItem(t('pages.admin.events.actions.feature'))
    await vi.waitFor(() => expect(notificationTexts().join('\n')).toContain(t('errors.generic')))
    expect(calls).toBe(2)
  })

  it('creates an event from the dialog and closes it', async () => {
    const bodies: unknown[] = []
    server.use(
      http.post('/api/events', async ({ request: req }) => {
        bodies.push(await req.json())
        return bodies.length === 1
          ? HttpResponse.json(buildAdminEvent(), { status: 201 })
          : apiError(409, 'EventNotFound')
      }),
    )
    const { wrapper } = await renderPage()

    const newButton = findButtons(t('pages.admin.events.newEvent'))[0]
    newButton?.click()
    await flushPromises()
    const dialog = wrapper.findComponent(EventFormDialog)
    expect(dialog.props('visible')).toBe(true)
    expect(dialog.props('event')).toBeNull()

    dialog.vm.$emit('submit', input)
    await vi.waitFor(() =>
      expect(notificationTexts().join('\n')).toContain(t('pages.admin.events.toasts.created')),
    )
    expect(bodies).toEqual([request])
    expect(dialog.props('visible')).toBe(false)

    newButton?.click()
    await flushPromises()
    dialog.vm.$emit('submit', input)
    await vi.waitFor(() => expect(bodies).toHaveLength(2))
    await vi.waitFor(() =>
      expect(notificationTexts().join('\n')).toContain(t('errors.EventNotFound')),
    )
    expect(notificationTexts().join('\n')).toContain('trace-123')
    expect(dialog.props('visible')).toBe(true)
  })

  it('loads the event detail for editing and saves the update', async () => {
    const puts: { id: string; body: unknown }[] = []
    server.use(
      http.get('/api/events/:eventId', () =>
        HttpResponse.json(buildAdminEvent({ title: 'Loaded' })),
      ),
      http.put('/api/events/:eventId', async ({ request: req, params }) => {
        puts.push({ id: String(params.eventId), body: await req.json() })
        return puts.length === 1 ? HttpResponse.json(buildAdminEvent()) : apiError(500)
      }),
    )
    const { wrapper } = await renderPage()

    rowButton(t('common.edit')).click()
    const dialog = wrapper.findComponent(EventFormDialog)
    await vi.waitFor(() => expect(dialog.props('visible')).toBe(true))
    expect(dialog.props('event')).toMatchObject({ title: 'Loaded' })

    dialog.vm.$emit('submit', input)
    await vi.waitFor(() =>
      expect(notificationTexts().join('\n')).toContain(t('pages.admin.events.toasts.updated')),
    )
    expect(puts).toEqual([{ id: EVENT_ID, body: request }])
    expect(dialog.props('visible')).toBe(false)

    rowButton(t('common.edit')).click()
    await vi.waitFor(() => expect(dialog.props('visible')).toBe(true))
    dialog.vm.$emit('submit', input)
    await vi.waitFor(() => expect(puts).toHaveLength(2))
    await flushPromises()
    expect(dialog.props('visible')).toBe(true)
  })

  it('does not open the dialog when the event detail cannot be loaded', async () => {
    server.use(http.get('/api/events/:eventId', () => apiError(500)))
    const { wrapper } = await renderPage()

    rowButton(t('common.edit')).click()

    await vi.waitFor(() => expect(notificationTexts().join('\n')).toContain(t('errors.generic')))
    expect(wrapper.findComponent(EventFormDialog).props('visible')).toBe(false)
  })

  it('keeps the dialog closed when the edited event no longer exists', async () => {
    server.use(http.get('/api/events/:eventId', () => apiError(404)))
    const { wrapper } = await renderPage()

    rowButton(t('common.edit')).click()

    await vi.waitFor(() =>
      expect(
        document.body.querySelector('.el-notification .el-notification__title')?.textContent,
      ).toBe(t('common.error')),
    )
    expect(wrapper.findComponent(EventFormDialog).props('visible')).toBe(false)
  })

  it('tells the admin when the edited event no longer exists', async () => {
    server.use(http.get('/api/events/:eventId', () => apiError(404)))
    const { wrapper } = await renderPage()

    rowButton(t('common.edit')).click()

    await vi.waitFor(() =>
      expect(notificationTexts().join('\n')).toContain(t('pages.admin.events.toasts.notFound')),
    )
    expect(wrapper.findComponent(EventFormDialog).props('visible')).toBe(false)
  })

  it('disables create and edit while an event detail is loading', async () => {
    let release: () => void = () => undefined
    let gets = 0
    server.use(
      http.get('/api/events/:eventId', async () => {
        gets += 1
        await new Promise<void>((resolve) => {
          release = resolve
        })
        return HttpResponse.json(buildAdminEvent())
      }),
    )
    const { wrapper } = await renderPage()

    rowButton(t('common.edit')).click()
    await vi.waitFor(() => expect(gets).toBe(1))
    await flushPromises()

    expect(rowButton(t('pages.admin.events.newEvent')).disabled).toBe(true)
    expect(rowButton(t('common.edit')).disabled).toBe(true)
    expect(wrapper.findComponent(EventFormDialog).props('visible')).toBe(false)

    release()
    await vi.waitFor(() =>
      expect(wrapper.findComponent(EventFormDialog).props('visible')).toBe(true),
    )
    expect(rowButton(t('common.edit')).disabled).toBe(false)
  })

  it('deletes an event from the row menu after confirmation and reports failures', async () => {
    const deleted: string[] = []
    server.use(
      http.delete('/api/events/:eventId', ({ params }) => {
        deleted.push(String(params.eventId))
        return deleted.length === 1 ? new HttpResponse(null, { status: 204 }) : apiError(500)
      }),
    )
    await renderPage()

    await chooseRowMenuItem(t('common.delete'))
    await vi.waitFor(() =>
      expect(document.body.textContent).toContain(
        t('pages.admin.events.delete.message', { title: 'Hackathon' }),
      ),
    )
    await confirmMessageBox()
    await vi.waitFor(() =>
      expect(notificationTexts().join('\n')).toContain(t('pages.admin.events.toasts.deleted')),
    )
    expect(deleted).toEqual([EVENT_ID])

    const boxes = document.body.querySelectorAll('.el-message-box').length
    await chooseRowMenuItem(t('common.delete'))
    await confirmMessageBox(boxes)
    await vi.waitFor(() => expect(deleted).toHaveLength(2))
    await vi.waitFor(() => expect(notificationTexts().join('\n')).toContain(t('errors.generic')))
  })
})
