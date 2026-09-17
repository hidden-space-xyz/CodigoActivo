import { flushPromises } from '@vue/test-utils'
import { ElPagination, ElTable } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { ActivityFormDialog } from '@/features/manage-activities'
import { EventDetailPage } from '@/pages/admin/event-detail'
import type {
  ActivityResponse,
  CreateActivityRequest,
  EventSummaryResponse,
} from '@/shared/api/generated/models'
import { ColumnFilterDate, ColumnFilterSelect, ColumnSearch } from '@/shared/ui'

import {
  bodyButtons,
  buildActivity,
  buildAttendee,
  buildEvent,
  buildRating,
  buildSummary,
  EVENT_ID,
  notificationsText,
  THUMBNAIL_ID,
  useCatalogHandlers,
} from '../../../../support/fixtures/admin-events/builders'
import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'

const request: CreateActivityRequest = {
  title: 'Robotics',
  description: 'Build robots',
  location: 'Room 1',
  activityModalityTypeId: 'mod-1',
  activityStartsAt: '2026-10-10T09:00:00.000Z',
  activityEndsAt: '2026-10-10T11:00:00.000Z',
  thumbnailId: THUMBNAIL_ID,
  roleCapacities: null,
}

interface DetailOptions {
  activities?: ActivityResponse[]
  summary?: EventSummaryResponse
  tableUrls?: URL[]
}

async function renderDetail(options: DetailOptions = {}) {
  const activities = options.activities ?? [buildActivity()]
  useCatalogHandlers()
  server.use(
    http.get('/api/events/:eventId', () => HttpResponse.json(buildEvent())),
    http.get('/api/reports/events/:eventId/summary', () =>
      HttpResponse.json(options.summary ?? buildSummary()),
    ),
    http.get('/api/activities', ({ request: req }) => {
      const url = new URL(req.url)
      if (url.searchParams.get('pageSize') !== '100') options.tableUrls?.push(url)
      return HttpResponse.json(paged(activities))
    }),
  )
  const rendered = await renderWithProviders(EventDetailPage, {
    route: `/admin/events/${EVENT_ID}`,
    attach: true,
  })
  await vi.waitFor(() => expect(rendered.wrapper.text()).toContain('Hackathon'))
  await flushPromises()
  return rendered
}

function button(label: string): HTMLButtonElement {
  const found = bodyButtons(label)[0]
  if (!found) throw new Error(`Button not found: ${label}`)
  return found
}

async function confirmMessageBox(previousBoxes = 0) {
  await vi.waitFor(() =>
    expect(document.body.querySelectorAll('.el-message-box').length).toBeGreaterThan(previousBoxes),
  )
  const box = [...document.body.querySelectorAll('.el-message-box')].at(-1)
  if (!box) throw new Error('Message box not found')
  const confirm = bodyButtons(t('common.delete'), box)[0]
  if (!confirm) throw new Error('Confirm button not found')
  confirm.click()
  await flushPromises()
}

describe('EventDetailPage', () => {
  it('shows the event header, summary cards and activities table', async () => {
    const { wrapper } = await renderDetail({
      activities: [
        buildActivity(),
        buildActivity({ id: 'act-2', title: 'Painting', modalityName: null, location: null }),
      ],
    })

    const text = wrapper.text()
    expect(text).toContain('Code all night')
    const cards = wrapper.findAll('.summary__card').map((card) => card.text())
    expect(cards[0]).toBe(`3${t('pages.admin.eventDetail.tabs.activities')}`)
    expect(cards[1]).toBe('7Volunteer')
    expect(cards[2]).toContain('4,3')
    expect(cards[2]).toContain('/5')
    expect(text).toContain('Robotics')
    expect(text).toContain('Painting')
    expect(text).toContain('On site')
    expect(text).toContain('Room 1')
  })

  it('falls back when the event, summary data and role names are missing', async () => {
    useCatalogHandlers()
    server.use(
      http.get('/api/events/:eventId', () => apiError(404)),
      http.get('/api/reports/events/:eventId/summary', () =>
        HttpResponse.json({
          ratingsCount: 0,
          ratingsAverage: null,
          roleTypeBreakdown: [{ roleTypeName: null }],
        } satisfies EventSummaryResponse),
      ),
      http.get('/api/activities', () => HttpResponse.json(paged([]))),
    )

    const { wrapper } = await renderWithProviders(EventDetailPage, {
      route: `/admin/events/${EVENT_ID}`,
    })

    await vi.waitFor(() =>
      expect(wrapper.text()).toContain(t('pages.admin.eventDetail.empty.none')),
    )
    expect(wrapper.text()).toContain(t('pages.admin.eventDetail.headerFallback'))
    const cards = wrapper.findAll('.summary__card').map((card) => card.text())
    expect(cards[0]).toBe(`0${t('pages.admin.eventDetail.tabs.activities')}`)
    expect(cards[1]).toBe('0—')
    expect(cards[2]).not.toContain('/5')
    expect(cards[2]).toContain('—')
  })

  it('shows an error when activities cannot be loaded', async () => {
    useCatalogHandlers()
    server.use(
      http.get('/api/events/:eventId', () => HttpResponse.json(buildEvent())),
      http.get('/api/reports/events/:eventId/summary', () => apiError(500)),
      http.get('/api/activities', () => apiError(500)),
    )

    const { wrapper } = await renderWithProviders(EventDetailPage, {
      route: `/admin/events/${EVENT_ID}`,
    })

    await vi.waitFor(() =>
      expect(wrapper.text()).toContain(t('pages.admin.eventDetail.empty.error')),
    )
  })

  it('opens the printable badges and roster pages', async () => {
    const { router } = await renderDetail()

    button(t('pages.admin.eventDetail.printBadges')).click()
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('admin-event-badges'))
    expect(router.currentRoute.value.params.eventId).toBe(EVENT_ID)

    button(t('pages.admin.eventDetail.printRoster')).click()
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('admin-event-roster'))
  })

  it('filters, sorts and pages the activities table', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderDetail({ tableUrls: urls })

    const search = wrapper.findComponent(ColumnSearch)
    search.vm.$emit('update:modelValue', 'robot')
    search.vm.$emit('apply')
    const dates = wrapper.findComponent(ColumnFilterDate)
    dates.vm.$emit('update:modelValue', ['2026-10-10', '2026-10-11'])
    dates.vm.$emit('apply')
    const modality = wrapper.findComponent(ColumnFilterSelect)
    expect(modality.props('options')).toEqual([
      { label: 'On site', value: 'mod-1' },
      { label: 'Online', value: 'mod-2' },
    ])
    modality.vm.$emit('update:modelValue', 'mod-2')

    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('modalityTypeId')).toBe('mod-2'))
    expect(urls.at(-1)?.searchParams.get('title')).toBe('robot')
    expect(urls.at(-1)?.searchParams.get('activityDateFrom')).toBe('2026-10-10')

    modality.vm.$emit('update:modelValue', true)
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.has('modalityTypeId')).toBe(false))

    wrapper.findComponent(ElTable).vm.$emit('sort-change', { prop: 'title', order: 'ascending' })
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('sort')).toBe('title'))

    const pagination = wrapper.findComponent(ElPagination)
    pagination.vm.$emit('update:page-size', 50)
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('pageSize')).toBe('50'))
    pagination.vm.$emit('update:current-page', 2)
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('page')).toBe('2'))
  })

  it('creates an activity and reports failures', async () => {
    const posts: { path: string; body: unknown }[] = []
    server.use(
      http.post('/api/activities/:eventId', async ({ request: req }) => {
        posts.push({ path: new URL(req.url).pathname, body: await req.json() })
        return posts.length === 1 ? HttpResponse.json(buildActivity()) : apiError(500)
      }),
    )
    const { wrapper } = await renderDetail()

    button(t('pages.admin.eventDetail.newActivity')).click()
    await flushPromises()
    const dialog = wrapper.findComponent(ActivityFormDialog)
    expect(dialog.props()).toMatchObject({
      visible: true,
      activity: null,
      eventStart: '2026-10-10',
      eventEnd: '2026-10-12',
    })

    dialog.vm.$emit('submit', request)
    await vi.waitFor(() =>
      expect(notificationsText()).toContain(t('pages.admin.eventDetail.toast.activityCreated')),
    )
    expect(posts).toEqual([{ path: `/api/activities/${EVENT_ID}`, body: request }])
    expect(dialog.props('visible')).toBe(false)

    button(t('pages.admin.eventDetail.newActivity')).click()
    await flushPromises()
    dialog.vm.$emit('submit', request)
    await vi.waitFor(() => expect(notificationsText()).toContain(t('errors.generic')))
    expect(dialog.props('visible')).toBe(true)

    dialog.vm.$emit('update:visible', false)
    await flushPromises()
    expect(dialog.props('visible')).toBe(false)
  })

  it('edits an activity with its freshest data and reports failures', async () => {
    const puts: { id: string; body: unknown }[] = []
    let detailStatus = 200
    server.use(
      http.get('/api/activities/:activityId', () =>
        detailStatus === 200
          ? HttpResponse.json(buildActivity({ title: 'Robotics (fresh)' }))
          : apiError(detailStatus),
      ),
      http.put('/api/activities/:activityId', async ({ request: req, params }) => {
        puts.push({ id: String(params.activityId), body: await req.json() })
        return puts.length === 1 ? HttpResponse.json(buildActivity()) : apiError(500)
      }),
    )
    const { wrapper } = await renderDetail()
    const dialog = wrapper.findComponent(ActivityFormDialog)

    button(t('common.edit')).click()
    await vi.waitFor(() =>
      expect(dialog.props('activity')).toMatchObject({ title: 'Robotics (fresh)' }),
    )
    expect(dialog.props('visible')).toBe(true)

    dialog.vm.$emit('submit', request)
    await vi.waitFor(() =>
      expect(notificationsText()).toContain(t('pages.admin.eventDetail.toast.activityUpdated')),
    )
    expect(puts).toEqual([{ id: 'act-1', body: request }])
    expect(dialog.props('visible')).toBe(false)

    detailStatus = 500
    button(t('common.edit')).click()
    await flushPromises()
    expect(dialog.props('activity')).toMatchObject({ title: 'Robotics' })
    dialog.vm.$emit('submit', request)
    await vi.waitFor(() => expect(notificationsText()).toContain(t('errors.generic')))
    expect(dialog.props('visible')).toBe(true)
  })

  it('keeps the row data when the activity no longer exists or has no id', async () => {
    let gets = 0
    server.use(
      http.get('/api/activities/:activityId', () => {
        gets += 1
        return apiError(404)
      }),
    )
    const { wrapper } = await renderDetail({
      activities: [
        buildActivity({ id: undefined, title: 'No id' }),
        buildActivity({ id: 'act-gone', title: 'Gone' }),
      ],
    })
    const dialog = wrapper.findComponent(ActivityFormDialog)

    bodyButtons(t('common.edit'))[1]?.click()
    await vi.waitFor(() => expect(gets).toBe(1))
    await flushPromises()
    expect(dialog.props('activity')).toMatchObject({ id: 'act-gone', title: 'Gone' })

    button(t('common.edit')).click()
    await flushPromises()

    expect(gets).toBe(1)
    expect(dialog.props('activity')).toMatchObject({ id: '', title: 'No id' })

    dialog.vm.$emit('submit', request)
    await flushPromises()

    button(t('common.delete')).click()
    await confirmMessageBox()
    expect(wrapper.text()).toContain('No id')
  })

  it('deletes an activity after confirmation and reports failures', async () => {
    const deleted: string[] = []
    server.use(
      http.delete('/api/activities/:activityId', ({ params }) => {
        deleted.push(String(params.activityId))
        return deleted.length === 1 ? new HttpResponse(null, { status: 204 }) : apiError(500)
      }),
    )
    await renderDetail()

    button(t('common.delete')).click()
    await vi.waitFor(() =>
      expect(document.body.textContent).toContain(
        t('pages.admin.eventDetail.deleteConfirm.message', { title: 'Robotics' }),
      ),
    )
    await confirmMessageBox()
    await vi.waitFor(() =>
      expect(notificationsText()).toContain(t('pages.admin.eventDetail.toast.activityDeleted')),
    )
    expect(deleted).toEqual(['act-1'])

    const boxes = document.body.querySelectorAll('.el-message-box').length
    button(t('common.delete')).click()
    await confirmMessageBox(boxes)
    await vi.waitFor(() => expect(notificationsText()).toContain(t('errors.generic')))
  })

  it('loads attendees and opinions only when their tabs are opened', async () => {
    let attendeeRequests = 0
    let ratingRequests = 0
    server.use(
      http.get('/api/reports/events/:eventId/attendees', () => {
        attendeeRequests += 1
        return HttpResponse.json(paged([buildAttendee()]))
      }),
      http.get('/api/events/:eventId/ratings', () => {
        ratingRequests += 1
        return HttpResponse.json(paged([buildRating()]))
      }),
    )
    const { wrapper } = await renderDetail()
    expect(attendeeRequests).toBe(0)
    expect(ratingRequests).toBe(0)

    await wrapper.find('#tab-attendees').trigger('click')
    await vi.waitFor(() => expect(wrapper.text()).toContain('Ada Lovelace'))
    expect(attendeeRequests).toBe(1)

    await wrapper.find('#tab-opinions').trigger('click')
    await vi.waitFor(() =>
      expect(wrapper.text()).toContain(t('pages.admin.eventDetail.opinions.anonymous')),
    )
    expect(ratingRequests).toBe(1)
  })
})
