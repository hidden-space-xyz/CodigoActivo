import { describe, expect, it, vi } from 'vitest'

import { useTermsDocumentsAdmin } from '@/pages/admin/catalogs/model/use-terms-documents-admin'

import { http, HttpResponse, noContent, paged, server } from '../../../../../support/server'
import { buildTermsDocument, richText } from '../../../../../support/builders'
import { expectNotification, queryOf } from '../../../../../support/dom'
import { mountComposable, t } from '../../../../../support/render'

describe('useTermsDocumentsAdmin', () => {
  it('lists the terms documents by name and creates or updates the one in the dialog', async () => {
    const urls: string[] = []
    const calls: string[] = []
    server.use(
      http.get('/api/events/termsDocument', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildTermsDocument()]))
      }),
      http.post('/api/events/termsDocument', () => {
        calls.push('POST')
        return HttpResponse.json(buildTermsDocument(), { status: 201 })
      }),
      http.put('/api/events/termsDocument/:id', ({ params }) => {
        calls.push(`PUT ${String(params.id)}`)
        return noContent()
      }),
    )
    const { result } = await mountComposable(() => useTermsDocumentsAdmin())
    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'name' })

    result.dialog.openCreate()
    result.save({ name: 'Cookies', description: richText('Body') })
    await expectNotification(t('pages.admin.catalogs.termsDocuments.toasts.created'))
    expect(result.dialog.visible.value).toBe(false)

    await result.dialog.openEdit({ id: 'terms-1', name: 'Privacy', description: richText('Body') })
    result.save({ name: 'Privacy', description: richText('New body') })
    await expectNotification(t('pages.admin.catalogs.termsDocuments.toasts.updated'))

    expect(calls).toEqual(['POST', 'PUT terms-1'])
  })
})
