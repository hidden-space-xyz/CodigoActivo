import { describe, expect, it } from 'vitest'

import type { ResourceInput } from '@/entities/resource'
import {
  createResourceRequest,
  deleteResourceRequest,
  getResourceListPageRequest,
  getResourceRequest,
  getResourcesPageRequest,
  getResourceTypesRequest,
  updateResourceRequest,
} from '@/entities/resource/api/requests'
import { ApiError } from '@/shared/api'

import { apiError, http, HttpResponse, noContent, paged, server } from '../../../../support/server'
import {
  buildResourceListItem,
  buildResourceResponse,
  externalType,
  internalType,
} from '../../../../support/builders'
import { queryOf } from '../../../../support/dom'

const INPUT: ResourceInput = {
  title: 'Nuevo',
  subtitle: 'Guía',
  description: null,
  url: 'https://vuejs.org',
  resourceTypeId: 'type-link',
  thumbnailId: 'thumb',
}

describe('resource requests', () => {
  it('pages public resources newest first, sending the search only when present', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/resources', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildResourceListItem()], 8))
      }),
    )

    const page = await getResourcesPageRequest('python', 1, 25)
    await getResourcesPageRequest('', 2, 25)

    expect(page).toEqual({
      items: [expect.objectContaining({ id: 'resource-1', title: 'Guía de Python', url: null })],
      total: 8,
    })
    expect(urls.map(queryOf)).toEqual([
      { search: 'python', sort: '-createdAt', page: '1', pageSize: '25' },
      { sort: '-createdAt', page: '2', pageSize: '25' },
    ])
  })

  it('loads a whole resource, resolving null when it does not exist', async () => {
    server.use(
      http.get('/api/resources/r1', () => HttpResponse.json(buildResourceResponse({ id: 'r1' }))),
      http.get('/api/resources/missing', () => apiError(404, 'ResourceNotFound')),
      http.get('/api/resources/broken', () => apiError(500)),
    )

    await expect(getResourceRequest('r1')).resolves.toMatchObject({ id: 'r1', url: null })
    await expect(getResourceRequest('missing')).resolves.toBeNull()
    await expect(getResourceRequest('broken')).rejects.toBeInstanceOf(ApiError)
  })

  it('lists the resource types', async () => {
    server.use(
      http.get('/api/resources/types', () => HttpResponse.json([internalType, externalType])),
    )

    await expect(getResourceTypesRequest()).resolves.toEqual([
      { id: 'type-article', name: 'Artículo', color: '#123456', isExternal: false },
      { id: 'type-link', name: 'Enlace', color: '', isExternal: true },
    ])
  })

  it('pages the admin list with mapped resources', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/resources', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildResourceListItem()], 4))
      }),
    )

    await expect(getResourceListPageRequest({ page: 1, sort: 'title' })).resolves.toEqual({
      items: [expect.objectContaining({ id: 'resource-1', createdAt: '2026-03-02T10:00:00Z' })],
      total: 4,
    })
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', sort: 'title' })
  })

  it('creates, replaces and deletes resources', async () => {
    const calls: { method: string; path: string; body: unknown }[] = []
    const record = async ({ request }: { request: Request }) => {
      const text = await request.text()
      calls.push({
        method: request.method,
        path: new URL(request.url).pathname,
        body: text ? (JSON.parse(text) as unknown) : null,
      })
      return request.method === 'DELETE' ? noContent() : HttpResponse.json(buildResourceResponse())
    }
    server.use(
      http.post('/api/resources', record),
      http.put('/api/resources/r1', record),
      http.delete('/api/resources/r1', record),
    )

    await createResourceRequest(INPUT)
    await updateResourceRequest('r1', { ...INPUT, title: 'Editado' })
    await deleteResourceRequest('r1')

    expect(calls).toEqual([
      { method: 'POST', path: '/api/resources', body: INPUT },
      { method: 'PUT', path: '/api/resources/r1', body: { ...INPUT, title: 'Editado' } },
      { method: 'DELETE', path: '/api/resources/r1', body: null },
    ])
  })
})
