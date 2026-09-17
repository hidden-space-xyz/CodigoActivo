import { describe, expect, it } from 'vitest'

import {
  createPartnerRequest,
  deletePartnerRequest,
  getPartnersPageRequest,
  updatePartnerRequest,
} from '@/entities/partner'
import { getSponsorsRequest } from '@/entities/partner/api/requests'
import type { PartnerResponse } from '@/shared/api/generated/models'

import { http, HttpResponse, paged, server } from '../../../../support/server'

describe('partner requests', () => {
  it('loads sponsors ordered by tier, dropping entries without id or name', async () => {
    let query: Record<string, string> = {}
    const partners: PartnerResponse[] = [
      { id: 'p1', name: 'Acme', website: 'https://acme.test', thumbnailId: 'logo-1', tier: 1 },
      { id: 'p2', name: 'Sin web' },
      { id: 'p3', name: '' },
      { name: 'Sin id' },
    ]
    server.use(
      http.get('/api/partners', ({ request }) => {
        query = Object.fromEntries(new URL(request.url).searchParams)
        return HttpResponse.json(paged(partners))
      }),
    )

    await expect(getSponsorsRequest()).resolves.toEqual([
      { id: 'p1', name: 'Acme', website: 'https://acme.test', thumbnailId: 'logo-1' },
      { id: 'p2', name: 'Sin web', website: '', thumbnailId: '' },
    ])
    expect(query).toEqual({ pageSize: '100', sort: 'tier,-fromDate' })
  })

  it('pages the admin table with raw partners', async () => {
    let query: Record<string, string> = {}
    server.use(
      http.get('/api/partners', ({ request }) => {
        query = Object.fromEntries(new URL(request.url).searchParams)
        return HttpResponse.json(paged([{ id: 'p1', tier: 2 }], 6))
      }),
    )

    await expect(getPartnersPageRequest({ page: 2, pageSize: 3 })).resolves.toEqual({
      items: [{ id: 'p1', tier: 2 }],
      total: 6,
    })
    expect(query).toEqual({ page: '2', pageSize: '3' })
  })

  it('creates, updates and deletes partners', async () => {
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
        : HttpResponse.json<PartnerResponse>({ id: 'p1', name: request.method })
    }
    server.use(
      http.post('/api/partners', record),
      http.put('/api/partners/p1', record),
      http.delete('/api/partners/p1', record),
    )

    await expect(createPartnerRequest({ name: 'Acme', tier: 1 })).resolves.toEqual({
      id: 'p1',
      name: 'POST',
    })
    await expect(updatePartnerRequest('p1', { name: 'Acme 2' })).resolves.toEqual({
      id: 'p1',
      name: 'PUT',
    })
    expect((await deletePartnerRequest('p1')).status).toBe(204)
    expect(calls).toEqual([
      { method: 'POST', path: '/api/partners', body: { name: 'Acme', tier: 1 } },
      { method: 'PUT', path: '/api/partners/p1', body: { name: 'Acme 2' } },
      { method: 'DELETE', path: '/api/partners/p1', body: null },
    ])
  })
})
