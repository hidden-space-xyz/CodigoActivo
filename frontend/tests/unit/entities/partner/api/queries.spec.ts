import { describe, expect, it } from 'vitest'

import { partnerKeys, partnerList, partnerQueries } from '@/entities/partner'

import { http, HttpResponse, paged, server } from '../../../../support/server'
import { buildPartnerResponse } from '../../../../support/builders'
import { queryOf } from '../../../../support/dom'
import { createTestQueryClient } from '../../../../support/render'

describe('partnerKeys', () => {
  it('shares the partners root between the sponsors and the admin list', () => {
    expect(partnerKeys.all).toEqual(['partners'])
    expect(partnerKeys.sponsors()).toEqual(['partners', 'sponsors'])
    expect(partnerKeys.list()).toEqual(['partners', 'list'])
  })
})

describe('partnerQueries.sponsors', () => {
  it('caches the sponsors under their key', async () => {
    server.use(http.get('/api/partners', () => HttpResponse.json(paged([buildPartnerResponse()]))))
    const client = createTestQueryClient()

    await expect(client.fetchQuery(partnerQueries.sponsors())).resolves.toEqual([
      {
        id: 'partner-1',
        name: 'Acme',
        website: 'https://acme.test',
        thumbnailId: 'thumb-partner-1',
      },
    ])
    expect(client.getQueryData(partnerKeys.sponsors())).toHaveLength(1)
  })
})

describe('partnerList', () => {
  it('pages the admin list under the list key with the table params', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/partners', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildPartnerResponse()], 30))
      }),
    )

    const page = await partnerList.fetchPage({ page: 2, pageSize: 25, sort: 'tier' })

    expect(partnerList.queryKey).toEqual(partnerKeys.list())
    expect(page.total).toBe(30)
    expect(page.items.map((partner) => partner.name)).toEqual(['Acme'])
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '2', pageSize: '25', sort: 'tier' })
  })
})
