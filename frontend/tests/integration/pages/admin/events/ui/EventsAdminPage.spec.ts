import { flushPromises } from '@vue/test-utils'
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
import { findButtons, notificationTexts } from '../../../../../support/dom'
import { ColumnFilterDate, ColumnFilterSelect, ColumnSearch } from '@/shared/ui/column-filter'

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
  it('lists events with their tags, categories, dates and early signup', async () => {
    const { wrapper } = await renderPage([
      buildAdminEventListItem({
        featured: true,
        earlySignupStartsAt: '2026-08-20T08:00:00Z',
      }),
      buildAdminEventListItem({ id: 'ev-2', title: 'Workshop', categories: [] }),
    ])

    await vi.waitFor(() => expect(wrapper.text()).toContain('Workshop'))
    const text = wrapper.text()
    expect(text).toContain(t('pages.admin.events.header.title'))
    expect(text).toContain('Hackathon')
    expect(text).toContain(t('pages.admin.events.tag.featured'))
    expect(text).toContain('Tech')
    expect(text).toContain('—')
    expect(text).toContain(t('pages.admin.events.earlySignupFrom', { date: '' }).trim())
    expect(findButtons(t('pages.admin.events.aria.featured'))[0]?.disabled).toBe(true)
    expect(findButtons(t('pages.admin.events.aria.feature'))[0]?.disabled).toBe(false)
    const manage = wrapper.find(`a[href="/admin/events/${EVENT_ID}"]`)
    expect(manage.exists()).toBe(true)
  })

  it('shows the empty and error messages', async () => {
    const { wrapper } = await renderPage([])
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

  it('applies the header column filters to the request', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderPage([buildAdminEventListItem()], urls)

    const [title, subtitle] = wrapper.findAllComponents(ColumnSearch)
    const [eventDate, signup] = wrapper.findAllComponents(ColumnFilterDate)
    title?.vm.$emit('update:modelValue', 'hack')
    title?.vm.$emit('apply')
    subtitle?.vm.$emit('update:modelValue', 'night')
    subtitle?.vm.$emit('apply')
    const category = wrapper.findComponent(ColumnFilterSelect)
    category.vm.$emit('update:modelValue', 'cat-2')
    category.vm.$emit('apply')
    eventDate?.vm.$emit('update:modelValue', ['2026-10-01', '2026-10-31'])
    eventDate?.vm.$emit('apply')
    signup?.vm.$emit('update:modelValue', ['2026-09-01', '2026-09-30'])
    signup?.vm.$emit('apply')

    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('signupTo')).toBe('2026-09-30'))
    const last = urls.at(-1)
    expect(last?.searchParams.get('title')).toBe('hack')
    expect(last?.searchParams.get('subtitle')).toBe('night')
    expect(last?.searchParams.get('categoryTypeId')).toBe('cat-2')
    expect(last?.searchParams.get('eventDateFrom')).toBe('2026-10-01')
    expect(last?.searchParams.get('eventDateTo')).toBe('2026-10-31')
    expect(last?.searchParams.get('signupFrom')).toBe('2026-09-01')
    expect(category.props('options')).toEqual([
      { label: 'Tech', value: 'cat-1' },
      { label: 'Art', value: 'cat-2' },
    ])
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

  it('features an event and reports failures', async () => {
    let calls = 0
    server.use(
      http.patch('/api/events/:eventId/feature', () => {
        calls += 1
        return calls === 1 ? HttpResponse.json(buildAdminEvent({ featured: true })) : apiError(500)
      }),
    )
    await renderPage()

    rowButton(t('pages.admin.events.aria.feature')).click()
    await vi.waitFor(() =>
      expect(notificationTexts().join('\n')).toContain(t('pages.admin.events.toasts.featured')),
    )

    rowButton(t('pages.admin.events.aria.feature')).click()
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

  it('deletes an event after confirmation and reports failures', async () => {
    const deleted: string[] = []
    server.use(
      http.delete('/api/events/:eventId', ({ params }) => {
        deleted.push(String(params.eventId))
        return deleted.length === 1 ? new HttpResponse(null, { status: 204 }) : apiError(500)
      }),
    )
    await renderPage()

    rowButton(t('common.delete')).click()
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
    rowButton(t('common.delete')).click()
    await confirmMessageBox(boxes)
    await vi.waitFor(() => expect(deleted).toHaveLength(2))
    await vi.waitFor(() => expect(notificationTexts().join('\n')).toContain(t('errors.generic')))
  })
})
