import { MutationObserver } from '@tanstack/vue-query'
import { describe, expect, it } from 'vitest'

import { eventCategoryKeys, eventCategoryMutations } from '@/entities/event-category'

import { apiError, http, HttpResponse, noContent, server } from '../../../../support/server'
import { buildEventCategoryType } from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

const INPUT = { name: 'Talk', color: '#00FF00' }

function cachedCategories() {
  const client = createTestQueryClient()
  const keys = [eventCategoryKeys.options(), [...eventCategoryKeys.list(), { page: 1 }]]
  for (const queryKey of keys) client.setQueryData(queryKey, [])
  const invalidated = () =>
    keys.map((queryKey) => client.getQueryState(queryKey)?.isInvalidated ?? false)
  return { client, invalidated }
}

describe('eventCategoryMutations', () => {
  it('resolves a created category and refreshes every category query after each change', async () => {
    server.use(
      http.post('/api/events/categoryType', () =>
        HttpResponse.json(buildEventCategoryType({ id: 'new' }), { status: 201 }),
      ),
      http.put('/api/events/categoryType/:id', () => noContent()),
      http.delete('/api/events/categoryType/:id', () => noContent()),
    )

    const created = cachedCategories()
    await expect(
      new MutationObserver(created.client, eventCategoryMutations.create()).mutate(INPUT),
    ).resolves.toMatchObject({ id: 'new' })
    const updated = cachedCategories()
    await new MutationObserver(updated.client, eventCategoryMutations.update()).mutate({
      id: 'cat-1',
      input: INPUT,
    })
    const removed = cachedCategories()
    await new MutationObserver(removed.client, eventCategoryMutations.remove()).mutate('cat-1')

    for (const { invalidated } of [created, updated, removed]) {
      expect(invalidated()).toEqual([true, true])
    }
  })

  it('leaves the category queries alone when the API refuses the change', async () => {
    server.use(http.delete('/api/events/categoryType/:id', () => apiError(500)))
    const { client, invalidated } = cachedCategories()

    await expect(
      new MutationObserver(client, eventCategoryMutations.remove()).mutate('cat-1'),
    ).rejects.toMatchObject({ status: 500 })

    expect(invalidated()).toEqual([false, false])
  })
})
