import { describe, expect, it } from 'vitest'

import {
  createPartnerRequest,
  deletePartnerRequest,
  getPartnersPageRequest,
  getSponsorsRequest,
  updatePartnerRequest,
} from '@/entities/partner/api/requests'
import type { PartnerInput } from '@/entities/partner'

import { http, HttpResponse, noContent, paged, server } from '../../../../support/server'
import { buildPartnerResponse } from '../../../../support/builders'
import { queryOf } from '../../../../support/dom'

const INPUT: PartnerInput = {
  name: 'Acme',
  fromDate: '2024-03-15',
  tier: 1,
  website: null,
  thumbnailId: 'thumb-1',
}

describe('partner requests', () => {
  it('loads sponsors ordered by tier, dropping entries without a name', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/partners', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(
          paged([
            buildPartnerResponse({ id: 'p1', name: 'Acme', thumbnailId: 'logo-1' }),
            buildPartnerResponse({ id: 'p2', name: 'Sin web', website: null }),
            buildPartnerResponse({ id: 'p3', name: '' }),
          ]),
        )
      }),
    )

    await expect(getSponsorsRequest()).resolves.toEqual([
      { id: 'p1', name: 'Acme', website: 'https://acme.test', thumbnailId: 'logo-1' },
      { id: 'p2', name: 'Sin web', website: '', thumbnailId: 'thumb-partner-1' },
    ])
    expect(queryOf(urls[0] ?? '')).toEqual({ pageSize: '100', sort: 'tier,-fromDate' })
  })

  it('pages the admin list with mapped partners', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/partners', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildPartnerResponse({ tier: 2 })], 6))
      }),
    )

    await expect(getPartnersPageRequest({ page: 2, pageSize: 3, tier: 2 })).resolves.toEqual({
      items: [expect.objectContaining({ id: 'partner-1', tier: 2 })],
      total: 6,
    })
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '2', pageSize: '3', tier: '2' })
  })

  it('creates, replaces and deletes partners', async () => {
    const calls: { method: string; path: string; body: unknown }[] = []
    const record = async ({ request }: { request: Request }) => {
      const text = await request.text()
      calls.push({
        method: request.method,
        path: new URL(request.url).pathname,
        body: text ? (JSON.parse(text) as unknown) : null,
      })
      return request.method === 'DELETE' ? noContent() : HttpResponse.json(buildPartnerResponse())
    }
    server.use(
      http.post('/api/partners', record),
      http.put('/api/partners/p1', record),
      http.delete('/api/partners/p1', record),
    )

    await createPartnerRequest(INPUT)
    await updatePartnerRequest('p1', { ...INPUT, name: 'Acme 2' })
    await deletePartnerRequest('p1')

    expect(calls).toEqual([
      { method: 'POST', path: '/api/partners', body: INPUT },
      { method: 'PUT', path: '/api/partners/p1', body: { ...INPUT, name: 'Acme 2' } },
      { method: 'DELETE', path: '/api/partners/p1', body: null },
    ])
  })
})
