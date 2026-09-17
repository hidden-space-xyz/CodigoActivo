import { describe, expect, it, vi } from 'vitest'

import { useAccountHistory } from '@/features/account/model/useAccountHistory'
import type { EventHistoryResponse } from '@/shared/api/generated/models'

import {
  buildHistoryActivityResponse,
  buildHistoryResponse,
  buildRatingResponse,
} from '../../../../support/fixtures/account/account'
import { apiError, http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../../../support/server'

import { withSetup } from './with-setup'

describe('useAccountHistory', () => {
  it('does not request the history without a signed-in user', async () => {
    const requested = vi.fn()
    server.use(
      http.get('/api/me/event-history', () => {
        requested()
        return HttpResponse.json([])
      }),
    )

    const { result } = await withSetup(() => useAccountHistory())

    expect(requested).not.toHaveBeenCalled()
    expect(result.entries.value).toEqual([])
    expect(result.upcoming.value).toEqual([])
    expect(result.past.value).toEqual([])
  })

  it('splits the mapped history into upcoming and past events', async () => {
    const entries: EventHistoryResponse[] = [
      buildHistoryResponse({ eventId: 'upcoming-1', isPast: false }),
      buildHistoryResponse({
        eventId: 'past-1',
        isPast: true,
        canRate: true,
        myRating: buildRatingResponse({ score: 5, mostLiked: null }),
        activities: [
          buildHistoryActivityResponse({
            userId: 'child-1',
            firstName: 'Byron',
            lastName: null,
            isSelf: false,
          }),
        ],
      }),
      { eventId: 'bare' },
    ]
    server.use(http.get('/api/me/event-history', () => HttpResponse.json(entries)))

    const { result } = await withSetup(() => useAccountHistory(), { user: {} })
    await vi.waitFor(() => expect(result.entries.value).toHaveLength(3))

    expect(result.upcoming.value.map((entry) => entry.eventId)).toEqual(['upcoming-1', 'bare'])
    expect(result.past.value.map((entry) => entry.eventId)).toEqual(['past-1'])

    const [past] = result.past.value
    expect(past?.rating).toEqual({
      score: 5,
      mostLiked: '',
      leastLiked: 'El calor',
      suggestions: 'Más agua',
    })
    expect(past?.activities[0]).toMatchObject({
      participantId: 'child-1',
      participantName: 'Byron',
      isSelf: false,
    })
    expect(result.upcoming.value[1]).toMatchObject({ rating: null, activities: [], title: '' })
  })

  it('saves a rating with trimmed answers and refetches the history', async () => {
    let historyRequests = 0
    let received: { body: unknown; csrf: string | null } | undefined
    server.use(
      http.get('/api/me/event-history', () => {
        historyRequests += 1
        return HttpResponse.json([buildHistoryResponse({ isPast: true, canRate: true })])
      }),
      http.put('/api/events/:eventId/rating', async ({ request, params }) => {
        received = { body: await request.json(), csrf: request.headers.get('X-CSRF-TOKEN') }
        expect(params.eventId).toBe('event-1')
        return HttpResponse.json(buildRatingResponse())
      }),
    )

    const { result } = await withSetup(() => useAccountHistory(), { user: {} })
    await vi.waitFor(() => expect(historyRequests).toBe(1))

    const saved = await result.saveRating.mutateAsync({
      eventId: 'event-1',
      input: { score: 4, mostLiked: '  Los talleres ', leastLiked: '   ', suggestions: '' },
    })

    expect(saved).toEqual({
      score: 4,
      mostLiked: 'Los talleres',
      leastLiked: 'El calor',
      suggestions: 'Más agua',
    })
    expect(received).toEqual({
      body: { score: 4, mostLiked: 'Los talleres', leastLiked: null, suggestions: null },
      csrf: TEST_CSRF_TOKEN,
    })
    await vi.waitFor(() => expect(historyRequests).toBe(2))
  })

  it('exposes the error when the rating cannot be saved', async () => {
    server.use(
      http.get('/api/me/event-history', () => HttpResponse.json([])),
      http.put('/api/events/:eventId/rating', () => apiError(400, 'EventNotFound')),
    )

    const { result } = await withSetup(() => useAccountHistory(), { user: {} })

    await expect(
      result.saveRating.mutateAsync({
        eventId: 'event-1',
        input: { score: 0, mostLiked: '', leastLiked: '', suggestions: '' },
      }),
    ).rejects.toMatchObject({ status: 400, code: 'EventNotFound' })
  })
})
