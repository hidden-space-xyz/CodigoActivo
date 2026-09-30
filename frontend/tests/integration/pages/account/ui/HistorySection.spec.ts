import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import HistorySection from '@/pages/account/ui/HistorySection.vue'
import type { EventHistoryResponse } from '@/shared/api/generated/models'
import { i18n } from '@/shared/i18n'

import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'
import { buildHistoryActivityResponse, buildHistoryResponse } from '../../../../support/builders'
import {
  click,
  findButton,
  notificationTexts,
  openDialog,
  openDialogs,
  typeInto,
} from '../../../../support/dom'
import { formatDateRange } from '@/shared/lib/date'

const UPCOMING = buildHistoryResponse({
  eventId: 'upcoming-1',
  title: 'Campus de verano',
  eventStartsAt: '2030-07-01',
  eventEndsAt: '2030-07-05',
  activities: [
    buildHistoryActivityResponse({ activityId: 'a1', statusName: 'Confirmada' }),
    buildHistoryActivityResponse({
      activityId: 'a2',
      userId: 'child-1',
      firstName: 'Byron',
      lastName: 'Lovelace',
      isSelf: false,
      roleTypeName: '',
      statusName: 'Rechazada',
    }),
    buildHistoryActivityResponse({ activityId: 'a3', statusName: 'Pendiente' }),
    buildHistoryActivityResponse({ activityId: 'a4', statusName: '' }),
  ],
})

const PAST_WITH_ACTIVITY = buildHistoryResponse({
  eventId: 'past-1',
  title: 'Hackathon de Primavera',
  isPast: true,
  canRate: true,
})

const PAST_WITHOUT_ACTIVITIES = buildHistoryResponse({
  eventId: 'past-2',
  title: 'Taller de otoño',
  isPast: true,
  canRate: true,
  activities: [],
})

function serveHistory(
  entries: EventHistoryResponse[] = [UPCOMING, PAST_WITH_ACTIVITY, PAST_WITHOUT_ACTIVITIES],
) {
  let requests = 0
  server.use(
    http.get('/api/me/event-history', () => {
      requests += 1
      return HttpResponse.json(entries)
    }),
  )
  return { count: () => requests }
}

async function renderSection() {
  const { wrapper } = await renderWithProviders(HistorySection, { user: {}, attach: true })
  await flushPromises()
  return wrapper
}

function eventItem(title: string): HTMLElement {
  const item = [...document.querySelectorAll<HTMLElement>('.acc-history__event')].find(
    (candidate) => candidate.querySelector('.acc-history__name')?.textContent.trim() === title,
  )
  if (!item) throw new Error(`No history entry "${title}"`)
  return item
}

describe('HistorySection', () => {
  it('shows a loading state and then the empty message', async () => {
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => {
      release = resolve
    })
    server.use(
      http.get('/api/me/event-history', async () => {
        await gate
        return HttpResponse.json([])
      }),
    )

    const wrapper = await renderSection()
    expect(wrapper.text()).toContain(t('common.loading'))

    release()
    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.account.history.empty')))
  })

  it('explains when the history cannot be loaded', async () => {
    server.use(http.get('/api/me/event-history', () => apiError(500)))

    const wrapper = await renderSection()

    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.account.history.error')))
  })

  it('groups events into upcoming and past with dates and activity counts', async () => {
    serveHistory()

    const wrapper = await renderSection()

    const groups = wrapper.findAll('.acc-history__group-title').map((title) => title.text())
    expect(groups).toEqual([t('pages.account.history.upcoming'), t('pages.account.history.past')])
    const upcoming = eventItem('Campus de verano')
    expect(upcoming.textContent).toContain(formatDateRange('2030-07-01', '2030-07-05'))
    expect(upcoming.textContent).toContain(i18n.global.t('pages.account.history.activityCount', 4))
    expect(eventItem('Taller de otoño').textContent).toContain(
      i18n.global.t('pages.account.history.activityCount', 0),
    )
    expect(upcoming.querySelector('.acc-history__actions')).toBeNull()
  })

  it('hides a group that has no events', async () => {
    serveHistory([PAST_WITH_ACTIVITY])

    const wrapper = await renderSection()

    expect(wrapper.findAll('.acc-history__group-title').map((title) => title.text())).toEqual([
      t('pages.account.history.past'),
    ])
  })

  it('expands an event to show household activities with roles and signup status', async () => {
    serveHistory()
    await renderSection()
    const upcoming = eventItem('Campus de verano')
    const toggle = upcoming.querySelector('.acc-history__toggle')
    if (!toggle) throw new Error('Missing toggle')

    expect(toggle.getAttribute('aria-expanded')).toBe('false')
    expect(upcoming.querySelector('.acc-history__activities')).toBeNull()

    await click(toggle)

    expect(toggle.getAttribute('aria-expanded')).toBe('true')
    const activities = [...upcoming.querySelectorAll('.acc-history__activity')]
    expect(activities).toHaveLength(4)
    expect(activities[0]?.querySelector('.acc-history__participant')).toBeNull()
    expect(activities[0]?.textContent).toContain('Participante')
    expect(activities[1]?.querySelector('.acc-history__participant')?.textContent.trim()).toBe(
      'Byron Lovelace',
    )
    const tags = activities.map((activity) => activity.querySelector('.el-tag')?.className ?? '')
    expect(tags[0]).toContain('el-tag--success')
    expect(tags[1]).toContain('el-tag--danger')
    expect(tags[2]).toContain('el-tag--info')
    expect(tags[3]).toBe('')

    await click(toggle)
    expect(upcoming.querySelector('.acc-history__activities')).toBeNull()
  })

  it('does not show signup status tags for past events', async () => {
    serveHistory()
    await renderSection()
    const past = eventItem('Hackathon de Primavera')

    await click(past.querySelector('.acc-history__toggle') as Element)

    expect(past.querySelectorAll('.acc-history__activity')).toHaveLength(1)
    expect(past.querySelector('.el-tag')).toBeNull()
  })

  it('offers the rate button on every past entry that can be rated', async () => {
    serveHistory()
    await renderSection()

    for (const title of ['Hackathon de Primavera', 'Taller de otoño']) {
      const entry = eventItem(title)
      expect(entry.querySelectorAll('.acc-history__actions button')).toHaveLength(1)
      expect(findButton(t('pages.account.history.rate'), entry)).toBeTruthy()
    }
  })

  it('keeps the rate button after the rating was saved and the history refreshed', async () => {
    const history = serveHistory()
    server.use(
      http.post('/api/events/:eventId/rating', () => new HttpResponse(null, { status: 204 })),
    )
    await renderSection()

    await click(findButton(t('pages.account.history.rate'), eventItem('Taller de otoño')))
    await click(findButton(t('common.save'), openDialog(t('pages.account.history.dialog.header'))))

    await vi.waitFor(() => expect(history.count()).toBe(2))
    expect(findButton(t('pages.account.history.rate'), eventItem('Taller de otoño'))).toBeTruthy()
  })

  it('opens the dialog empty, saves the rating, confirms it and refreshes the history', async () => {
    const history = serveHistory()
    let received: { eventId: unknown; body: unknown } | undefined
    server.use(
      http.post('/api/events/:eventId/rating', async ({ params, request }) => {
        received = { eventId: params.eventId, body: await request.json() }
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await renderSection()

    await click(findButton(t('pages.account.history.rate'), eventItem('Taller de otoño')))
    const dialog = openDialog(t('pages.account.history.dialog.header'))
    expect(dialog.textContent).toContain('Taller de otoño')
    expect(dialog.textContent).toContain(t('pages.account.history.dialog.anonymousNotice'))
    expect(dialog.querySelector<HTMLTextAreaElement>('#rating-most')?.value).toBe('')
    await click(dialog.querySelectorAll('.el-rate__item')[2] as Element)
    await typeInto('#rating-most', '  Los retos  ', dialog)
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(received).toBeDefined())
    expect(received).toEqual({
      eventId: 'past-2',
      body: { score: 3, mostLiked: 'Los retos', leastLiked: null, suggestions: null },
    })
    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
    expect(notificationTexts().join()).toContain(t('pages.account.history.savedSummary'))
    expect(notificationTexts().join()).toContain(t('pages.account.history.savedDetail'))
    await vi.waitFor(() => expect(history.count()).toBe(2))
  })

  it('keeps the dialog open and notifies when the rating cannot be saved', async () => {
    serveHistory()
    server.use(http.post('/api/events/:eventId/rating', () => apiError(404, 'EventNotFound')))
    await renderSection()

    await click(findButton(t('pages.account.history.rate'), eventItem('Taller de otoño')))
    const dialog = openDialog(t('pages.account.history.dialog.header'))
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() => expect(notificationTexts()).toHaveLength(1))
    expect(notificationTexts()[0]).toContain(t('errors.EventNotFound'))
    expect(notificationTexts()[0]).toContain('trace-123')
    expect(openDialogs()).toHaveLength(1)
  })

  it('closes the rating dialog without saving when cancelled', async () => {
    serveHistory()
    await renderSection()

    await click(findButton(t('pages.account.history.rate'), eventItem('Taller de otoño')))
    await click(
      findButton(t('common.cancel'), openDialog(t('pages.account.history.dialog.header'))),
    )

    expect(openDialogs()).toHaveLength(0)
  })

  it('ignores a rating submitted for an event without an identifier', async () => {
    const saved = vi.fn()
    serveHistory([
      buildHistoryResponse({ eventId: '', title: 'Sin id', isPast: true, canRate: true }),
    ])
    server.use(
      http.post('/api/events/:eventId/rating', () => {
        saved()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await renderSection()

    await click(findButton(t('pages.account.history.rate'), eventItem('Sin id')))
    await click(findButton(t('common.save'), openDialog(t('pages.account.history.dialog.header'))))

    expect(saved).not.toHaveBeenCalled()
    expect(openDialogs()).toHaveLength(1)
  })
})
