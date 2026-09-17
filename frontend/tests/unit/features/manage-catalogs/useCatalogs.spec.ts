import { describe, expect, it, vi } from 'vitest'

import { useCatalog } from '@/features/manage-catalogs/model/useCatalog'
import { useEventCategories } from '@/features/manage-catalogs/model/useEventCategories'
import { useTermsDocuments } from '@/features/manage-catalogs/model/useTermsDocuments'

import {
  buildEventCategory,
  buildTermsDocument,
} from '../../../support/fixtures/admin-content/builders'
import { queryOf, withSetup } from '../../../support/fixtures/admin-content/helpers'
import { http, HttpResponse, paged, server } from '../../../support/server'

describe('useCatalog', () => {
  it('invalidates the catalog key only after successful mutations', async () => {
    const api = {
      queryKey: ['catalogs', 'things'] as const,
      create: vi.fn((body: { name: string }) => Promise.resolve(body)),
      update: vi.fn(() => Promise.resolve()),
      remove: vi.fn(() => Promise.reject(new Error('in use'))),
    }
    const { result, queryClient } = await withSetup(() => useCatalog(api))
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    await result.create.mutateAsync({ name: 'A' })
    await result.update.mutateAsync({ id: 'thing-1', body: { name: 'B' } })
    await expect(result.remove.mutateAsync('thing-1')).rejects.toThrow('in use')

    expect(api.create).toHaveBeenCalledWith({ name: 'A' })
    expect(api.update).toHaveBeenCalledWith('thing-1', { name: 'B' })
    expect(api.remove).toHaveBeenCalledWith('thing-1')
    expect(invalidate).toHaveBeenCalledTimes(2)
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['catalogs', 'things'] })
  })
})

describe('useEventCategories', () => {
  it('pages categories by name and sends CRUD requests that refresh the category lists', async () => {
    const urls: string[] = []
    const requests: unknown[] = []
    server.use(
      http.get('/api/events/categoryType', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildEventCategory()]))
      }),
      http.post('/api/events/categoryType', async ({ request }) => {
        requests.push({ method: 'POST', body: await request.json() })
        return HttpResponse.json(buildEventCategory())
      }),
      http.put('/api/events/categoryType/:id', async ({ request, params }) => {
        requests.push({ method: 'PUT', id: params.id, body: await request.json() })
        return new HttpResponse(null, { status: 204 })
      }),
      http.delete('/api/events/categoryType/:id', ({ params }) => {
        requests.push({ method: 'DELETE', id: params.id })
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { result, queryClient } = await withSetup(() => useEventCategories())
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'name' })

    await result.create.mutateAsync({ name: 'Talk', color: '#000000' })
    await result.update.mutateAsync({ id: 'category-1', body: { name: 'Talk', color: '#FFFFFF' } })
    await result.remove.mutateAsync('category-1')

    expect(requests).toEqual([
      { method: 'POST', body: { name: 'Talk', color: '#000000' } },
      { method: 'PUT', id: 'category-1', body: { name: 'Talk', color: '#FFFFFF' } },
      { method: 'DELETE', id: 'category-1' },
    ])
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['catalogs', 'event-category-types'] })
  })
})

describe('useTermsDocuments', () => {
  it('pages terms documents by name and sends CRUD requests', async () => {
    const urls: string[] = []
    const requests: string[] = []
    server.use(
      http.get('/api/events/termsDocument', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildTermsDocument()]))
      }),
      http.post('/api/events/termsDocument', () => {
        requests.push('POST')
        return HttpResponse.json(buildTermsDocument())
      }),
      http.put('/api/events/termsDocument/:id', ({ params }) => {
        requests.push(`PUT ${String(params.id)}`)
        return new HttpResponse(null, { status: 204 })
      }),
      http.delete('/api/events/termsDocument/:id', ({ params }) => {
        requests.push(`DELETE ${String(params.id)}`)
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { result, queryClient } = await withSetup(() => useTermsDocuments())
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'name' })

    const body = { name: 'Privacy', description: '{}' }
    await result.create.mutateAsync(body)
    await result.update.mutateAsync({ id: 'terms-1', body })
    await result.remove.mutateAsync('terms-1')

    expect(requests).toEqual(['POST', 'PUT terms-1', 'DELETE terms-1'])
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['catalogs', 'terms-documents'] })
  })
})
