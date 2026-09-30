import { MutationObserver } from '@tanstack/vue-query'
import { describe, expect, it } from 'vitest'

import { eventKeys, eventMutations, type EventInput } from '@/entities/event'

import { apiError, http, HttpResponse, noContent, server } from '../../../../support/server'
import { buildEventResponse } from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

type TestQueryClient = ReturnType<typeof createTestQueryClient>

const INPUT: EventInput = {
  title: 'Día',
  subtitle: 'Sub',
  description: '<p>Texto</p>',
  startsAt: '2026-10-03',
  endsAt: '2026-10-04',
  earlySignupStartsAt: null,
  signupStartsAt: '2026-09-10T10:00:00Z',
  signupEndsAt: '2026-09-30T10:00:00Z',
  thumbnailId: 'thumb-1',
  categoryIds: ['cat-1'],
  terms: [],
}

function cachedEvents() {
  const client = createTestQueryClient()
  const keys = [eventKeys.list(), eventKeys.home(), eventKeys.detail('e1')]
  for (const queryKey of keys) client.setQueryData(queryKey, [])
  const invalidated = () =>
    keys.map((queryKey) => client.getQueryState(queryKey)?.isInvalidated ?? false)
  return { client, invalidated }
}

describe('eventMutations', () => {
  it('refreshes every event query after creating, replacing, featuring or deleting', async () => {
    server.use(
      http.post('/api/events', () => HttpResponse.json(buildEventResponse(), { status: 201 })),
      http.put('/api/events/:id', () => HttpResponse.json(buildEventResponse())),
      http.patch('/api/events/:id/feature', () => HttpResponse.json(buildEventResponse())),
      http.delete('/api/events/:id', () => noContent()),
    )
    const runs = [
      (client: TestQueryClient) =>
        new MutationObserver(client, eventMutations.create()).mutate(INPUT),
      (client: TestQueryClient) =>
        new MutationObserver(client, eventMutations.update()).mutate({ id: 'e1', input: INPUT }),
      (client: TestQueryClient) =>
        new MutationObserver(client, eventMutations.feature()).mutate('e1'),
      (client: TestQueryClient) =>
        new MutationObserver(client, eventMutations.remove()).mutate('e1'),
    ]

    for (const run of runs) {
      const { client, invalidated } = cachedEvents()
      await run(client)
      expect(invalidated()).toEqual([true, true, true])
    }
  })

  it('leaves the event queries alone when the API refuses the change', async () => {
    server.use(http.delete('/api/events/:id', () => apiError(500)))
    const { client, invalidated } = cachedEvents()

    await expect(
      new MutationObserver(client, eventMutations.remove()).mutate('e1'),
    ).rejects.toMatchObject({ status: 500 })

    expect(invalidated()).toEqual([false, false, false])
  })
})
