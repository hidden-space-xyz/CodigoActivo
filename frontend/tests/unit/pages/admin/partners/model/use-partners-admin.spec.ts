import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import type { PartnerInput } from '@/entities/partner'
import { usePartnersAdmin } from '@/pages/admin/partners/model/use-partners-admin'

import { http, HttpResponse, paged, server } from '../../../../../support/server'
import { buildPartnerResponse } from '../../../../../support/builders'
import { expectNotification, queryOf } from '../../../../../support/dom'
import { buildPartner } from '../../../../../support/models'
import { mountComposable, t } from '../../../../../support/render'

const INPUT: PartnerInput = {
  name: 'Initech',
  fromDate: '2025-01-01',
  tier: 2,
  website: null,
  thumbnailId: 'thumb',
}

describe('usePartnersAdmin', () => {
  it('requests partners by tier and sends tier filters as numbers', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/partners', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildPartnerResponse()]))
      }),
    )
    const { result } = await mountComposable(() => usePartnersAdmin())

    await vi.waitFor(() => expect(result.table.total.value).toBe(1))
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'tier' })

    result.table.columnFilter('tier').value = 'not a number'
    await flushPromises()
    expect(result.table.filterParams.value).toEqual({})

    result.table.columnFilter('tier').value = '3'
    await flushPromises()
    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({ tier: '3' })
  })

  it('creates a partner when none is edited and replaces the edited one, closing the dialog', async () => {
    const calls: string[] = []
    server.use(
      http.get('/api/partners', () => HttpResponse.json(paged([]))),
      http.post('/api/partners', () => {
        calls.push('POST')
        return HttpResponse.json(buildPartnerResponse(), { status: 201 })
      }),
      http.put('/api/partners/:id', ({ params }) => {
        calls.push(`PUT ${String(params.id)}`)
        return HttpResponse.json(buildPartnerResponse())
      }),
    )
    const { result } = await mountComposable(() => usePartnersAdmin())

    result.dialog.openCreate()
    result.save(INPUT)
    await expectNotification(t('pages.admin.partners.toasts.created'))
    expect(result.dialog.visible.value).toBe(false)

    await result.dialog.openEdit(buildPartner({ id: 'partner-5' }))
    result.save(INPUT)
    await expectNotification(t('pages.admin.partners.toasts.updated'))
    expect(result.dialog.visible.value).toBe(false)

    expect(calls).toEqual(['POST', 'PUT partner-5'])
  })
})
