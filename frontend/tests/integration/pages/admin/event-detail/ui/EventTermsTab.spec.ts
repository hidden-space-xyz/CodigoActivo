import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import EventTermsTab from '@/pages/admin/event-detail/ui/EventTermsTab.vue'
import type { EventTermsDecisionResponse } from '@/shared/api/generated/models'
import { formatDateTime } from '@/shared/lib/date'

import { click, findDialog, textOf } from '../../../../../support/dom'
import { renderWithProviders, t } from '../../../../../support/render'
import { apiError, http, HttpResponse, paged, server } from '../../../../../support/server'
import { buildTermsDecision, EVENT_ID } from '../../../../../support/builders'

const DOCUMENTS = [
  { id: 'terms-1', name: 'Autorización de imagen', required: true, displayOrder: 0 },
  { id: 'terms-2', name: 'Normas', required: false, displayOrder: 1 },
]

async function renderTab(decisions: EventTermsDecisionResponse[], urls: URL[] = []) {
  server.use(
    http.get('/api/events/:eventId/terms/decisions', ({ request }) => {
      urls.push(new URL(request.url))
      return HttpResponse.json(paged(decisions))
    }),
  )
  const rendered = await renderWithProviders(EventTermsTab, {
    props: { eventId: EVENT_ID, documents: DOCUMENTS, active: true },
    attach: true,
  })
  await flushPromises()
  return rendered
}

function signatureAlt(name: string, document: string): string {
  return t('pages.admin.eventDetail.terms.signatureAlt', { name, document })
}

describe('EventTermsTab', () => {
  it('lists each decision with the person, the document, the outcome and the signature', async () => {
    const { wrapper } = await renderTab([
      buildTermsDecision(),
      buildTermsDecision({
        userId: 'user-2',
        firstName: 'Bea',
        lastName: 'Gil',
        email: null,
        termsDocumentId: 'terms-2',
        documentName: 'Normas',
        accepted: false,
        hasSignature: false,
      }),
    ])

    await vi.waitFor(() => expect(wrapper.findAll('.el-table__body tr')).toHaveLength(2))
    const [signed, declined] = wrapper.findAll('.el-table__body tr')
    expect(signed?.text()).toContain('Ada Lovelace')
    expect(signed?.text()).toContain('ada@example.com')
    expect(signed?.text()).toContain('Autorización de imagen')
    expect(signed?.text()).toContain(t('pages.admin.eventDetail.terms.accepted'))
    expect(signed?.text()).toContain(formatDateTime('2026-09-01T10:00:00Z'))
    const thumbnail = signed?.find('.signature-thumb img')
    expect(thumbnail?.attributes('src')).toBe(
      `/api/events/${EVENT_ID}/terms/decisions/user-1/terms-1/signature`,
    )
    expect(thumbnail?.attributes('alt')).toBe(
      signatureAlt('Ada Lovelace', 'Autorización de imagen'),
    )
    expect(declined?.text()).toContain(t('pages.admin.eventDetail.terms.rejected'))
    expect(declined?.text()).toContain(t('pages.admin.eventDetail.terms.noSignature'))
    expect(declined?.find('.signature-thumb').exists()).toBe(false)
  })

  it('shows a signature in full when its thumbnail is opened', async () => {
    const { wrapper } = await renderTab([buildTermsDecision()])
    await vi.waitFor(() => expect(wrapper.find('.signature-thumb').exists()).toBe(true))

    await click(wrapper.find('.signature-thumb').element)

    const dialog = findDialog(
      t('pages.admin.eventDetail.terms.signatureOf', { name: 'Ada Lovelace' }),
    )
    expect(dialog?.querySelector('img')?.getAttribute('src')).toBe(
      `/api/events/${EVENT_ID}/terms/decisions/user-1/terms-1/signature`,
    )
    expect(textOf(dialog?.querySelector('figcaption') ?? null)).toContain('Autorización de imagen')
  })

  it('offers the documents of the event in the document filter', async () => {
    const { wrapper } = await renderTab([])

    const headers = wrapper.findAll('.el-table__header th').map((cell) => cell.text())
    expect(headers).toEqual(
      expect.arrayContaining([
        t('pages.admin.eventDetail.terms.columns.person'),
        t('pages.admin.eventDetail.terms.columns.document'),
        t('pages.admin.eventDetail.terms.columns.signature'),
      ]),
    )
    expect(wrapper.text()).toContain(t('pages.admin.eventDetail.terms.empty'))
  })

  it('tells when the decisions cannot be loaded', async () => {
    server.use(
      http.get('/api/events/:eventId/terms/decisions', () => apiError(500, 'UnexpectedError')),
    )
    const { wrapper } = await renderWithProviders(EventTermsTab, {
      props: { eventId: EVENT_ID, documents: DOCUMENTS, active: true },
    })

    await vi.waitFor(() =>
      expect(wrapper.text()).toContain(t('pages.admin.eventDetail.terms.error')),
    )
  })
})
