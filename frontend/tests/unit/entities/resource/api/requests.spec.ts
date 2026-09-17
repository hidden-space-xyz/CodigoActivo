import { describe, expect, it } from 'vitest'

import {
  createResourceRequest,
  deleteResourceRequest,
  getResourceAdminRequest,
  getResourcesAdminPageRequest,
  updateResourceRequest,
} from '@/entities/resource'
import { getResourceByIdRequest, getResourcesPageRequest } from '@/entities/resource/api/requests'
import { ApiError } from '@/shared/api'
import type { ResourceResponse } from '@/shared/api/generated/models'

import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'

function queryOf(request: Request): Record<string, string> {
  return Object.fromEntries(new URL(request.url).searchParams)
}

describe('resource requests', () => {
  it('pages resources newest first and sends the search only when present', async () => {
    const queries: Record<string, string>[] = []
    server.use(
      http.get('/api/resources', ({ request }) => {
        queries.push(queryOf(request))
        return HttpResponse.json(paged([{ id: 'r1', title: 'Scratch' }], 8))
      }),
    )

    const page = await getResourcesPageRequest('python', 1, 25)
    await getResourcesPageRequest('', 2, 25)

    expect(page).toEqual({
      items: [expect.objectContaining({ id: 'r1', title: 'Scratch', url: null })],
      total: 8,
    })
    expect(queries).toEqual([
      { search: 'python', sort: '-createdAt', page: '1', pageSize: '25' },
      { sort: '-createdAt', page: '2', pageSize: '25' },
    ])
  })

  it('maps the public detail and returns the raw admin resource', async () => {
    const resource: ResourceResponse = { id: 'r1', title: 'Scratch', description: 'Texto' }
    server.use(http.get('/api/resources/r1', () => HttpResponse.json(resource)))

    await expect(getResourceByIdRequest('r1')).resolves.toMatchObject({
      id: 'r1',
      description: 'Texto',
    })
    await expect(getResourceAdminRequest('r1')).resolves.toEqual(resource)
  })

  it('resolves null for a missing resource and rethrows other errors', async () => {
    server.use(
      http.get('/api/resources/missing', () => apiError(404, 'ResourceNotFound')),
      http.get('/api/resources/broken', () => apiError(500)),
    )

    await expect(getResourceByIdRequest('missing')).resolves.toBeNull()
    await expect(getResourceAdminRequest('missing')).resolves.toBeNull()
    await expect(getResourceByIdRequest('broken')).rejects.toBeInstanceOf(ApiError)
  })

  it('pages the admin table with raw items', async () => {
    let query: Record<string, string> = {}
    server.use(
      http.get('/api/resources', ({ request }) => {
        query = queryOf(request)
        return HttpResponse.json(paged([{ id: 'r1' }], 4))
      }),
    )

    await expect(getResourcesAdminPageRequest({ page: 1, sort: 'title' })).resolves.toEqual({
      items: [{ id: 'r1' }],
      total: 4,
    })
    expect(query).toEqual({ page: '1', sort: 'title' })
  })

  it('creates, updates and deletes resources', async () => {
    const calls: { method: string; path: string; body: unknown }[] = []
    const record = async ({ request }: { request: Request }) => {
      const text = await request.text()
      calls.push({
        method: request.method,
        path: new URL(request.url).pathname,
        body: text ? (JSON.parse(text) as unknown) : null,
      })
      return request.method === 'DELETE'
        ? new HttpResponse(null, { status: 204 })
        : HttpResponse.json<ResourceResponse>({ id: 'r1', title: request.method })
    }
    server.use(
      http.post('/api/resources', record),
      http.put('/api/resources/r1', record),
      http.delete('/api/resources/r1', record),
    )

    await expect(createResourceRequest({ title: 'Nuevo', resourceTypeId: 't1' })).resolves.toEqual({
      id: 'r1',
      title: 'POST',
    })
    await expect(updateResourceRequest('r1', { title: 'Editado' })).resolves.toEqual({
      id: 'r1',
      title: 'PUT',
    })
    expect((await deleteResourceRequest('r1')).status).toBe(204)
    expect(calls).toEqual([
      { method: 'POST', path: '/api/resources', body: { title: 'Nuevo', resourceTypeId: 't1' } },
      { method: 'PUT', path: '/api/resources/r1', body: { title: 'Editado' } },
      { method: 'DELETE', path: '/api/resources/r1', body: null },
    ])
  })
})
