import { MutationObserver } from '@tanstack/vue-query'
import { describe, expect, it } from 'vitest'

import { partnerKeys, partnerMutations, type PartnerInput } from '@/entities/partner'

import { apiError, http, HttpResponse, noContent, server } from '../../../../support/server'
import { buildPartnerResponse } from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

const INPUT: PartnerInput = {
  name: 'Initech',
  fromDate: '2025-01-01',
  tier: 2,
  website: null,
  thumbnailId: 'thumb',
}

function cachedPartners() {
  const client = createTestQueryClient()
  client.setQueryData(partnerKeys.sponsors(), [])
  client.setQueryData([...partnerKeys.list(), { page: 1 }], { items: [], total: 0 })
  const invalidated = () =>
    [partnerKeys.sponsors(), [...partnerKeys.list(), { page: 1 }]].map(
      (queryKey) => client.getQueryState(queryKey)?.isInvalidated,
    )
  return { client, invalidated }
}

describe('partnerMutations', () => {
  it('refreshes every partner query after creating, replacing or deleting a partner', async () => {
    const calls: string[] = []
    server.use(
      http.post('/api/partners', () => {
        calls.push('POST')
        return HttpResponse.json(buildPartnerResponse(), { status: 201 })
      }),
      http.put('/api/partners/:id', ({ params }) => {
        calls.push(`PUT ${String(params.id)}`)
        return HttpResponse.json(buildPartnerResponse())
      }),
      http.delete('/api/partners/:id', ({ params }) => {
        calls.push(`DELETE ${String(params.id)}`)
        return noContent()
      }),
    )

    for (const run of [
      (client: ReturnType<typeof createTestQueryClient>) =>
        new MutationObserver(client, partnerMutations.create()).mutate(INPUT),
      (client: ReturnType<typeof createTestQueryClient>) =>
        new MutationObserver(client, partnerMutations.update()).mutate({ id: 'p5', input: INPUT }),
      (client: ReturnType<typeof createTestQueryClient>) =>
        new MutationObserver(client, partnerMutations.remove()).mutate('p5'),
    ]) {
      const { client, invalidated } = cachedPartners()
      await run(client)
      expect(invalidated()).toEqual([true, true])
    }
    expect(calls).toEqual(['POST', 'PUT p5', 'DELETE p5'])
  })

  it('leaves the partner queries alone when the API refuses the change', async () => {
    server.use(http.delete('/api/partners/:id', () => apiError(500)))
    const { client, invalidated } = cachedPartners()

    await expect(
      new MutationObserver(client, partnerMutations.remove()).mutate('p5'),
    ).rejects.toMatchObject({ status: 500 })

    expect(invalidated()).toEqual([false, false])
  })
})
