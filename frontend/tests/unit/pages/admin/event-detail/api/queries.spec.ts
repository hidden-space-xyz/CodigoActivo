import { describe, expect, it } from 'vitest'

import {
  attendeeList,
  eventReportKeys,
  eventReportQueries,
  ratingList,
} from '@/pages/admin/event-detail/api/queries'

import { http, HttpResponse, paged, server } from '../../../../../support/server'
import { buildAttendee, buildRating, buildSummary } from '../../../../../support/builders'
import { queryOf } from '../../../../../support/dom'
import { createTestQueryClient } from '../../../../../support/render'

describe('eventReportKeys', () => {
  it('nests every event report under the reports root', () => {
    expect(eventReportKeys.all).toEqual(['reports'])
    expect(eventReportKeys.stats('e1')).toEqual(['reports', 'event-stats', 'e1'])
    expect(eventReportKeys.attendees()).toEqual(['reports', 'event-attendees'])
    expect(eventReportKeys.ratings()).toEqual(['reports', 'event-ratings'])
  })
})

describe('eventReportQueries', () => {
  it('loads the counts of an event', async () => {
    server.use(http.get('/api/reports/events/e1/summary', () => HttpResponse.json(buildSummary())))

    await expect(
      createTestQueryClient().fetchQuery(eventReportQueries.stats('e1')),
    ).resolves.toMatchObject({ activitiesCount: 3, scoredRatingsCount: 2 })
  })
})

describe('event report sources', () => {
  it('pages the attendees of the event in the params, sending the rest as query', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/reports/events/:eventId/attendees', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildAttendee()], 7))
      }),
    )

    const page = await attendeeList.fetchPage({
      eventId: 'e1',
      search: 'ada',
      gender: 'Female',
      page: 2,
      pageSize: 25,
      sort: 'lastName',
    })

    expect(page.total).toBe(7)
    expect(page.items[0]?.userId).toBe('user-1')
    expect(new URL(urls[0] ?? '').pathname).toBe('/api/reports/events/e1/attendees')
    expect(queryOf(urls[0] ?? '')).toEqual({
      search: 'ada',
      gender: 'Female',
      page: '2',
      pageSize: '25',
      sort: 'lastName',
    })
    expect(attendeeList.queryKey).toEqual(eventReportKeys.attendees())
  })

  it('pages the ratings of the event', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/events/:eventId/ratings', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildRating()], 1))
      }),
    )

    await expect(ratingList.fetchPage({ eventId: 'e1', sort: '-score' })).resolves.toEqual({
      items: [expect.objectContaining({ id: 'rating-1' })],
      total: 1,
    })
    expect(new URL(urls[0] ?? '').pathname).toBe('/api/events/e1/ratings')
    expect(queryOf(urls[0] ?? '')).toEqual({ sort: '-score' })
    expect(ratingList.queryKey).toEqual(eventReportKeys.ratings())
  })
})
