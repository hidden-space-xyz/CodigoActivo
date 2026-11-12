import { describe, expect, it } from 'vitest'

import {
  inventoryCategoryList,
  inventoryEntryList,
  inventoryKeys,
  inventoryQueries,
} from '@/entities/inventory'

import { http, HttpResponse, paged, server } from '../../../../support/server'
import {
  buildInventoryCategoryResponse,
  buildInventoryEntryResponse,
  buildInventorySummaryResponse,
} from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

describe('inventoryKeys', () => {
  it('shares the inventory root between the categories and their entries', () => {
    expect(inventoryKeys.list()).toEqual(['inventory', 'list'])
    expect(inventoryKeys.category('c1')).toEqual(['inventory', 'category', 'c1'])
    expect(inventoryKeys.entries('c1')).toEqual(['inventory', 'entries', 'c1'])
  })
})

describe('inventoryQueries', () => {
  it('caches a category and its entries under their keys', async () => {
    server.use(
      http.get('/api/inventory/categories/category-1', () =>
        HttpResponse.json(buildInventoryCategoryResponse()),
      ),
      http.get('/api/inventory/categories/category-1/entries', () =>
        HttpResponse.json([buildInventoryEntryResponse()]),
      ),
    )
    const client = createTestQueryClient()

    await client.fetchQuery(inventoryQueries.category('category-1'))
    await client.fetchQuery(inventoryQueries.entries('category-1'))

    expect(client.getQueryData(inventoryKeys.category('category-1'))).toMatchObject({
      name: 'Camisetas',
    })
    expect(client.getQueryData(inventoryKeys.entries('category-1'))).toHaveLength(1)
  })
})

describe('inventory sources', () => {
  it('pages the categories under the list key', async () => {
    server.use(
      http.get('/api/inventory/categories', () =>
        HttpResponse.json(paged([buildInventorySummaryResponse()])),
      ),
    )

    expect(inventoryCategoryList.queryKey).toEqual(inventoryKeys.list())
    await expect(inventoryCategoryList.fetchPage({ page: 1 })).resolves.toMatchObject({ total: 1 })
  })

  it('hands every entry to the select function with the table params', async () => {
    server.use(
      http.get('/api/inventory/categories/category-1/entries', () =>
        HttpResponse.json([
          buildInventoryEntryResponse(),
          buildInventoryEntryResponse({ id: 'e2' }),
        ]),
      ),
    )
    const source = inventoryEntryList('category-1', (entries, params: { page: number }) => ({
      items: entries.slice(params.page),
      total: entries.length,
    }))

    expect(source.queryKey).toEqual(inventoryKeys.entries('category-1'))
    await expect(source.fetchPage({ page: 1 })).resolves.toEqual({
      items: [expect.objectContaining({ id: 'e2' })],
      total: 2,
    })
  })
})
