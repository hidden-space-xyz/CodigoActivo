import { MutationObserver } from '@tanstack/vue-query'
import { describe, expect, it } from 'vitest'

import {
  inventoryKeys,
  inventoryMutations,
  type InventoryCategoryInput,
} from '@/entities/inventory'

import { http, HttpResponse, noContent, server } from '../../../../support/server'
import { createTestQueryClient } from '../../../../support/render'

const CATEGORY: InventoryCategoryInput = {
  name: 'Robots',
  columns: [{ id: null, name: 'Tipo', type: 'Text', required: true, options: [] }],
}
const ENTRY = { quantity: null, values: { type: 'mBot' } }

describe('inventoryMutations', () => {
  it('refreshes every inventory query after each change', async () => {
    const calls: string[] = []
    const answer = ({ request }: { request: Request }) => {
      calls.push(`${request.method} ${new URL(request.url).pathname}`)
      return request.method === 'DELETE' ? noContent() : HttpResponse.json({})
    }
    server.use(
      http.post('/api/inventory/categories', answer),
      http.put('/api/inventory/categories/:id', answer),
      http.delete('/api/inventory/categories/:id', answer),
      http.post('/api/inventory/categories/:id/entries', answer),
      http.put('/api/inventory/categories/:id/entries/:entryId', answer),
      http.delete('/api/inventory/categories/:id/entries/:entryId', answer),
    )
    type Client = ReturnType<typeof createTestQueryClient>

    for (const run of [
      (client: Client) =>
        new MutationObserver(client, inventoryMutations.createCategory()).mutate(CATEGORY),
      (client: Client) =>
        new MutationObserver(client, inventoryMutations.updateCategory()).mutate({
          id: 'c1',
          input: CATEGORY,
        }),
      (client: Client) =>
        new MutationObserver(client, inventoryMutations.removeCategory()).mutate('c1'),
      (client: Client) =>
        new MutationObserver(client, inventoryMutations.addEntry('c1')).mutate(ENTRY),
      (client: Client) =>
        new MutationObserver(client, inventoryMutations.updateEntry('c1')).mutate({
          id: 'e1',
          input: ENTRY,
        }),
      (client: Client) =>
        new MutationObserver(client, inventoryMutations.removeEntry('c1')).mutate('e1'),
    ]) {
      const client = createTestQueryClient()
      client.setQueryData(inventoryKeys.entries('c1'), [])
      await run(client)
      expect(client.getQueryState(inventoryKeys.entries('c1'))?.isInvalidated).toBe(true)
    }

    expect(calls).toEqual([
      'POST /api/inventory/categories',
      'PUT /api/inventory/categories/c1',
      'DELETE /api/inventory/categories/c1',
      'POST /api/inventory/categories/c1/entries',
      'PUT /api/inventory/categories/c1/entries/e1',
      'DELETE /api/inventory/categories/c1/entries/e1',
    ])
  })
})
