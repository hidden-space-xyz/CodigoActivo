import { ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useEventTermsDecisions } from '@/pages/admin/event-detail/model/use-event-terms-decisions'

import { http, HttpResponse, paged, server } from '../../../../../support/server'
import { buildTermsDecision } from '../../../../../support/builders'
import { queryOf } from '../../../../../support/dom'
import { mountComposable } from '../../../../../support/render'

describe('useEventTermsDecisions', () => {
  it('lists the decisions of the event latest first, only while the tab is active', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/events/:eventId/terms/decisions', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildTermsDecision()]))
      }),
    )
    const active = ref(false)

    const { result } = await mountComposable(() =>
      useEventTermsDecisions(
        () => 'event-1',
        () => active.value,
      ),
    )
    await flushPromises()
    expect(urls).toEqual([])

    active.value = true
    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))
    expect(new URL(urls[0] ?? '').pathname).toBe('/api/events/event-1/terms/decisions')
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: '-decidedAt' })
  })

  it('sends the person search and the document filter as their API params', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/events/:eventId/terms/decisions', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([]))
      }),
    )
    const { result } = await mountComposable(() =>
      useEventTermsDecisions(
        () => 'event-1',
        () => true,
      ),
    )
    await flushPromises()

    result.table.columnFilter('person').value = 'ada'
    result.table.columnFilter('document').value = 'terms-1'
    result.table.onFilter()
    await vi.waitFor(() => expect(urls).toHaveLength(2))

    expect(queryOf(urls[1] ?? '')).toMatchObject({ search: 'ada', termsDocumentId: 'terms-1' })
  })

  it('opens the signature of a decision', async () => {
    server.use(http.get('/api/events/:eventId/terms/decisions', () => HttpResponse.json(paged([]))))
    const { result } = await mountComposable(() =>
      useEventTermsDecisions(
        () => 'event-1',
        () => false,
      ),
    )
    const decision = {
      userId: 'user-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: '',
      termsDocumentId: 'terms-1',
      documentName: 'Autorización',
      accepted: true,
      decidedAt: '2026-09-01T10:00:00Z',
      signatureUrl: '/api/events/event-1/terms/decisions/user-1/terms-1/signature',
    }

    result.openSignature(decision)

    expect(result.signature).toEqual({ visible: true, decision })
  })
})
