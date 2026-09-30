import { MutationObserver } from '@tanstack/vue-query'
import { describe, expect, it } from 'vitest'

import { newsKeys, newsMutations, type NewsItemInput } from '@/entities/news-item'

import { apiError, http, HttpResponse, noContent, server } from '../../../../support/server'
import { buildNewsItemResponse, richText } from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

const INPUT: NewsItemInput = {
  title: 'Demo day',
  subtitle: 'Friday',
  description: richText('Details'),
  thumbnailId: 'thumb',
}

function cachedNews() {
  const client = createTestQueryClient()
  const keys = [newsKeys.home(), newsKeys.detail('a1'), newsKeys.list()]
  for (const queryKey of keys) client.setQueryData(queryKey, [])
  const invalidated = () =>
    keys.map((queryKey) => client.getQueryState(queryKey)?.isInvalidated ?? false)
  return { client, invalidated }
}

describe('newsMutations', () => {
  it('refreshes every news query after creating, replacing, featuring or deleting', async () => {
    server.use(
      http.post('/api/news', () => HttpResponse.json(buildNewsItemResponse(), { status: 201 })),
      http.put('/api/news/:id', () => HttpResponse.json(buildNewsItemResponse())),
      http.patch('/api/news/:id/feature', () => noContent()),
      http.delete('/api/news/:id', () => noContent()),
    )
    const runs = [
      (client: ReturnType<typeof createTestQueryClient>) =>
        new MutationObserver(client, newsMutations.create()).mutate(INPUT),
      (client: ReturnType<typeof createTestQueryClient>) =>
        new MutationObserver(client, newsMutations.update()).mutate({ id: 'a1', input: INPUT }),
      (client: ReturnType<typeof createTestQueryClient>) =>
        new MutationObserver(client, newsMutations.feature()).mutate('a1'),
      (client: ReturnType<typeof createTestQueryClient>) =>
        new MutationObserver(client, newsMutations.remove()).mutate('a1'),
    ]

    for (const run of runs) {
      const { client, invalidated } = cachedNews()
      await run(client)
      expect(invalidated()).toEqual([true, true, true])
    }
  })

  it('leaves the news queries alone when the API refuses the change', async () => {
    server.use(http.patch('/api/news/:id/feature', () => apiError(500)))
    const { client, invalidated } = cachedNews()

    await expect(
      new MutationObserver(client, newsMutations.feature()).mutate('a1'),
    ).rejects.toMatchObject({ status: 500 })

    expect(invalidated()).toEqual([false, false, false])
  })
})
