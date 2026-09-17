import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import HistorySection from '@/features/account/ui/HistorySection.vue'
import type { EventHistoryResponse } from '@/shared/api/generated/models'
import { i18n } from '@/shared/i18n'
import { formatDateRange } from '@/shared/lib'

import {
  buildHistoryActivityResponse,
  buildHistoryResponse,
  buildRatingResponse,
} from '../../../../support/fixtures/account/account'
import {
  buttonByText,
  click,
  dialogByTitle,
  fill,
  notificationTexts,
  openDialogs,
} from '../../../../support/fixtures/account/dom'
import { renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../support/server'

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
      roleTypeName: null,
      statusName: 'Rechazada',
    }),
    buildHistoryActivityResponse({ activityId: 'a3', statusName: 'Pendiente' }),
    buildHistoryActivityResponse({ activityId: 'a4', statusName: null }),
  ],
})

const PAST_RATED = buildHistoryResponse({
  eventId: 'past-1',
  title: 'Hackathon de Primavera',
  isPast: true,
  canRate: true,
  myRating: buildRatingResponse({ score: 4 }),
})

const PAST_UNRATED = buildHistoryResponse({
  eventId: 'past-2',
  title: 'Taller de otoño',
  isPast: true,
  canRate: true,
  activities: [],
})

function serveHistory(entries: EventHistoryResponse[] = [UPCOMING, PAST_RATED, PAST_UNRATED]) {
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
    await vi.waitFor(() => expect(wrapper.text()).toContain(t('features.account.history.empty')))
  })

  it('explains when the history cannot be loaded', async () => {
    server.use(http.get('/api/me/event-history', () => apiError(500)))

    const wrapper = await renderSection()

    await vi.waitFor(() => expect(wrapper.text()).toContain(t('features.account.history.error')))
  })

  it('groups events into upcoming and past with dates and activity counts', async () => {
    serveHistory()

    const wrapper = await renderSection()

    const groups = wrapper.findAll('.acc-history__group-title').map((title) => title.text())
    expect(groups).toEqual([
      t('features.account.history.upcoming'),
      t('features.account.history.past'),
    ])
    const upcoming = eventItem('Campus de verano')
    expect(upcoming.textContent).toContain(formatDateRange('2030-07-01', '2030-07-05'))
    expect(upcoming.textContent).toContain(
      i18n.global.t('features.account.history.activityCount', 4),
    )
    expect(eventItem('Taller de otoño').textContent).toContain(
      i18n.global.t('features.account.history.activityCount', 0),
    )
    expect(upcoming.querySelector('.acc-history__actions')).toBeNull()
  })

  it('hides a group that has no events', async () => {
    serveHistory([PAST_RATED])

    const wrapper = await renderSection()

    expect(wrapper.findAll('.acc-history__group-title').map((title) => title.text())).toEqual([
      t('features.account.history.past'),
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

  it('colours accepted, approved, denied and cancelled signups by outcome', async () => {
    const statuses = ['Aceptada', 'Aprobada', 'Denegada', 'Cancelada']
    serveHistory([
      buildHistoryResponse({
        title: 'Estados',
        activities: statuses.map((statusName, index) =>
          buildHistoryActivityResponse({ activityId: `s${String(index)}`, statusName }),
        ),
      }),
    ])
    await renderSection()
    const entry = eventItem('Estados')

    await click(entry.querySelector('.acc-history__toggle') as Element)

    const tags = [...entry.querySelectorAll('.el-tag')].map((tag) => tag.className)
    expect(tags).toHaveLength(4)
    expect(tags[0]).toContain('el-tag--success')
    expect(tags[1]).toContain('el-tag--success')
    expect(tags[2]).toContain('el-tag--danger')
    expect(tags[3]).toContain('el-tag--danger')
  })

  it('does not show signup status tags for past events', async () => {
    serveHistory()
    await renderSection()
    const past = eventItem('Hackathon de Primavera')

    await click(past.querySelector('.acc-history__toggle') as Element)

    expect(past.querySelectorAll('.acc-history__activity')).toHaveLength(1)
    expect(past.querySelector('.el-tag')).toBeNull()
  })

  it('shows the existing score and offers to edit or add ratings', async () => {
    serveHistory()
    await renderSection()

    const rated = eventItem('Hackathon de Primavera')
    expect(rated.querySelector('.acc-history__score')?.textContent.trim()).toBe('4/5')
    expect(buttonByText(rated, t('features.account.history.editRating'))).toBeTruthy()

    const unrated = eventItem('Taller de otoño')
    expect(unrated.querySelector('.acc-history__score')).toBeNull()
    expect(buttonByText(unrated, t('features.account.history.rate'))).toBeTruthy()
  })

  it('saves a rating, confirms it and refreshes the history', async () => {
    const history = serveHistory()
    let received: { eventId: unknown; body: unknown } | undefined
    server.use(
      http.put('/api/events/:eventId/rating', async ({ params, request }) => {
        received = { eventId: params.eventId, body: await request.json() }
        return HttpResponse.json(buildRatingResponse({ eventId: 'past-2', score: 3 }))
      }),
    )
    await renderSection()

    await click(buttonByText(eventItem('Taller de otoño'), t('features.account.history.rate')))
    const dialog = dialogByTitle(t('features.account.history.dialog.header'))
    expect(dialog.textContent).toContain('Taller de otoño')
    await click(dialog.querySelectorAll('.el-rate__item')[2] as Element)
    await fill(dialog, '#rating-most', '  Los retos  ')
    await click(buttonByText(dialog, t('common.save')))

    await vi.waitFor(() => expect(received).toBeDefined())
    expect(received).toEqual({
      eventId: 'past-2',
      body: { score: 3, mostLiked: 'Los retos', leastLiked: null, suggestions: null },
    })
    await vi.waitFor(() => expect(openDialogs()).toHaveLength(0))
    expect(notificationTexts().join()).toContain(t('features.account.history.savedSummary'))
    expect(notificationTexts().join()).toContain(t('features.account.history.savedDetail'))
    await vi.waitFor(() => expect(history.count()).toBe(2))
  })

  it('keeps the dialog open and notifies when the rating cannot be saved', async () => {
    serveHistory()
    server.use(http.put('/api/events/:eventId/rating', () => apiError(404, 'EventNotFound')))
    await renderSection()

    await click(
      buttonByText(eventItem('Hackathon de Primavera'), t('features.account.history.editRating')),
    )
    const dialog = dialogByTitle(t('features.account.history.dialog.header'))
    expect(dialog.querySelector<HTMLTextAreaElement>('#rating-most')?.value).toBe('Los talleres')
    await click(buttonByText(dialog, t('common.save')))

    await vi.waitFor(() => expect(notificationTexts()).toHaveLength(1))
    expect(notificationTexts()[0]).toContain(t('errors.EventNotFound'))
    expect(notificationTexts()[0]).toContain('trace-123')
    expect(openDialogs()).toHaveLength(1)
  })

  it('closes the rating dialog without saving when cancelled', async () => {
    serveHistory()
    await renderSection()

    await click(buttonByText(eventItem('Taller de otoño'), t('features.account.history.rate')))
    await click(
      buttonByText(dialogByTitle(t('features.account.history.dialog.header')), t('common.cancel')),
    )

    expect(openDialogs()).toHaveLength(0)
  })

  it('ignores a rating submitted for an event without an identifier', async () => {
    const saved = vi.fn()
    serveHistory([
      buildHistoryResponse({ eventId: '', title: 'Sin id', isPast: true, canRate: true }),
    ])
    server.use(
      http.put('/api/events/:eventId/rating', () => {
        saved()
        return HttpResponse.json(buildRatingResponse())
      }),
    )
    await renderSection()

    await click(buttonByText(eventItem('Sin id'), t('features.account.history.rate')))
    await click(
      buttonByText(dialogByTitle(t('features.account.history.dialog.header')), t('common.save')),
    )

    expect(saved).not.toHaveBeenCalled()
    expect(openDialogs()).toHaveLength(1)
  })
})
