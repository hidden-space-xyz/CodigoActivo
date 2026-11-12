import { describe, expect, it } from 'vitest'

import {
  addInventoryEntryRequest,
  createInventoryCategoryRequest,
  deleteInventoryCategoryRequest,
  deleteInventoryEntryRequest,
  getInventoryCategoryPageRequest,
  getInventoryCategoryRequest,
  getInventoryEntriesRequest,
  updateInventoryCategoryRequest,
  updateInventoryEntryRequest,
} from '@/entities/inventory/api/requests'
import type { InventoryCategoryInput } from '@/entities/inventory'

import { apiError, http, HttpResponse, noContent, paged, server } from '../../../../support/server'
import {
  buildInventoryCategoryResponse,
  buildInventoryEntryResponse,
  buildInventorySummaryResponse,
} from '../../../../support/builders'
import { queryOf } from '../../../../support/dom'

const CATEGORY: InventoryCategoryInput = {
  name: 'Camisetas',
  columns: [
    {
      id: null,
      name: 'Talla',
      type: 'List',
      required: true,
      options: [{ id: 'size-s', name: 'S', color: '#112233' }],
    },
  ],
}

describe('inventory requests', () => {
  it('pages the categories with their totals', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/inventory/categories', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildInventorySummaryResponse()], 4))
      }),
    )

    await expect(getInventoryCategoryPageRequest({ page: 2, name: 'cam' })).resolves.toEqual({
      items: [{ id: 'category-1', name: 'Camisetas', entryCount: 2, total: 42 }],
      total: 4,
    })
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '2', name: 'cam' })
  })

  it('loads a category with its columns, or null when it does not exist', async () => {
    server.use(
      http.get('/api/inventory/categories/:id', ({ params }) =>
        params.id === 'category-1'
          ? HttpResponse.json(buildInventoryCategoryResponse())
          : apiError(404, 'InventoryCategoryNotFound'),
      ),
    )

    const category = await getInventoryCategoryRequest('category-1')
    expect(category?.columns.map((column) => column.type)).toEqual(['List', 'Text', 'Quantity'])
    expect(category?.columns[0]?.options[1]).toEqual({ id: 'size-m', name: 'M', color: '#445566' })
    await expect(getInventoryCategoryRequest('missing')).resolves.toBeNull()
  })

  it('loads every entry, a missing quantity as null', async () => {
    server.use(
      http.get('/api/inventory/categories/category-1/entries', () =>
        HttpResponse.json([buildInventoryEntryResponse(), { id: 'entry-2', values: {} }]),
      ),
    )

    await expect(getInventoryEntriesRequest('category-1')).resolves.toEqual([
      { id: 'entry-1', quantity: 30, values: { size: 'size-s', color: 'Negra' } },
      { id: 'entry-2', quantity: null, values: {} },
    ])
  })

  it('creates, redefines and deletes categories and their entries', async () => {
    const calls: { method: string; path: string; body: unknown }[] = []
    const record = async ({ request }: { request: Request }) => {
      const text = await request.text()
      calls.push({
        method: request.method,
        path: new URL(request.url).pathname,
        body: text ? (JSON.parse(text) as unknown) : null,
      })
      return request.method === 'DELETE' ? noContent() : HttpResponse.json({})
    }
    server.use(
      http.post('/api/inventory/categories', record),
      http.put('/api/inventory/categories/:id', record),
      http.delete('/api/inventory/categories/:id', record),
      http.post('/api/inventory/categories/:id/entries', record),
      http.put('/api/inventory/categories/:id/entries/:entryId', record),
      http.delete('/api/inventory/categories/:id/entries/:entryId', record),
    )
    const entry = { quantity: 3, values: { size: 'size-s' } }

    await createInventoryCategoryRequest(CATEGORY)
    await updateInventoryCategoryRequest('category-1', CATEGORY)
    await deleteInventoryCategoryRequest('category-1')
    await addInventoryEntryRequest('category-1', entry)
    await updateInventoryEntryRequest('category-1', 'entry-1', entry)
    await deleteInventoryEntryRequest('category-1', 'entry-1')

    expect(calls).toEqual([
      { method: 'POST', path: '/api/inventory/categories', body: CATEGORY },
      { method: 'PUT', path: '/api/inventory/categories/category-1', body: CATEGORY },
      { method: 'DELETE', path: '/api/inventory/categories/category-1', body: null },
      { method: 'POST', path: '/api/inventory/categories/category-1/entries', body: entry },
      {
        method: 'PUT',
        path: '/api/inventory/categories/category-1/entries/entry-1',
        body: entry,
      },
      {
        method: 'DELETE',
        path: '/api/inventory/categories/category-1/entries/entry-1',
        body: null,
      },
    ])
  })
})
