import { MutationObserver } from '@tanstack/vue-query'
import { describe, expect, it } from 'vitest'

import { resourceKeys, resourceMutations, type ResourceInput } from '@/entities/resource'

import { apiError, http, HttpResponse, noContent, server } from '../../../../support/server'
import { buildResourceResponse } from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

const INPUT: ResourceInput = {
  title: 'Vue',
  subtitle: 'Docs',
  description: null,
  url: 'https://vuejs.org',
  resourceTypeId: 'type-link',
  thumbnailId: 'thumb',
}

function cachedResources() {
  const client = createTestQueryClient()
  const keys = [resourceKeys.pages(''), resourceKeys.detail('r1'), resourceKeys.types()]
  for (const queryKey of keys) client.setQueryData(queryKey, [])
  const invalidated = () =>
    keys.map((queryKey) => client.getQueryState(queryKey)?.isInvalidated ?? false)
  return { client, invalidated }
}

describe('resourceMutations', () => {
  it('refreshes every resource query, but not their types, after each change', async () => {
    server.use(
      http.post('/api/resources', () =>
        HttpResponse.json(buildResourceResponse(), { status: 201 }),
      ),
      http.put('/api/resources/:id', () => HttpResponse.json(buildResourceResponse())),
      http.delete('/api/resources/:id', () => noContent()),
    )

    const created = cachedResources()
    await new MutationObserver(created.client, resourceMutations.create()).mutate(INPUT)
    const updated = cachedResources()
    await new MutationObserver(updated.client, resourceMutations.update()).mutate({
      id: 'r1',
      input: INPUT,
    })
    const removed = cachedResources()
    await new MutationObserver(removed.client, resourceMutations.remove()).mutate('r1')

    for (const { invalidated } of [created, updated, removed]) {
      expect(invalidated()).toEqual([true, true, false])
    }
  })

  it('leaves the resource queries alone when the API refuses the change', async () => {
    server.use(http.delete('/api/resources/:id', () => apiError(500)))
    const { client, invalidated } = cachedResources()

    await expect(
      new MutationObserver(client, resourceMutations.remove()).mutate('r1'),
    ).rejects.toMatchObject({ status: 500 })

    expect(invalidated()).toEqual([false, false, false])
  })
})
