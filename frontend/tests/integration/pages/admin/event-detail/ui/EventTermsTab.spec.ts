import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import EventTermsTab from '@/pages/admin/event-detail/ui/EventTermsTab.vue'
import type { EventTermsDecisionResponse } from '@/shared/api/generated/models'
import { formatDateTime } from '@/shared/lib/date'
import { ColumnFilterSelect } from '@/shared/ui/column-filter'
import { SearchInput } from '@/shared/ui/search-input'

import {
  acceptMessageBox,
  click,
  expectNotification,
  findButton,
  findButtons,
  findDialog,
  messageBox,
  textOf,
} from '../../../../../support/dom'
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
    expect(signed?.find('.table-primary-cell__title').text()).toBe('Ada Lovelace')
    expect(signed?.find('.table-primary-cell__subtitle').text()).toBe('ada@example.com')
    expect(declined?.find('.table-primary-cell__title').text()).toBe('Bea Gil')
    expect(declined?.find('.table-primary-cell__subtitle').exists()).toBe(false)
    expect(signed?.text()).toContain('Autorización de imagen')
    expect(signed?.text()).toContain(t('pages.admin.eventDetail.terms.decidedBySelf'))
    expect(declined?.text()).toContain('Ada Lovelace')
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
    expect(wrapper.findComponent(ColumnFilterSelect).props('options')).toEqual([
      { label: 'Autorización de imagen', value: 'terms-1' },
      { label: 'Normas', value: 'terms-2' },
    ])
    expect(wrapper.text()).toContain(t('pages.admin.eventDetail.terms.empty'))
  })

  it('searches people from the toolbar, filters by document and sorts by first name', async () => {
    const urls: URL[] = []
    const { wrapper } = await renderTab([buildTermsDecision()], urls)

    const search = wrapper.findComponent(SearchInput)
    expect(search.props('label')).toBe(t('pages.admin.eventDetail.terms.columns.searchPerson'))
    search.vm.$emit('update:modelValue', 'ada')
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('search')).toBe('ada'))

    const documentFilter = wrapper.findComponent(ColumnFilterSelect)
    documentFilter.vm.$emit('update:modelValue', 'terms-2')
    documentFilter.vm.$emit('apply')
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('termsDocumentId')).toBe('terms-2'))
    expect(urls.at(-1)?.searchParams.get('search')).toBe('ada')
    await vi.waitFor(() =>
      expect(wrapper.findAll('.filter-chip__label').map((chip) => chip.text())).toEqual([
        t('table.filterChip', {
          label: t('pages.admin.eventDetail.terms.columns.document'),
          value: 'Normas',
        }),
      ]),
    )

    const person = wrapper.get('.el-table__header th')
    expect(person.text()).toBe(t('pages.admin.eventDetail.terms.columns.person'))
    await click(person.get('.sort-caret.ascending').element)
    await vi.waitFor(() => expect(urls.at(-1)?.searchParams.get('sort')).toBe('firstName'))
  })

  it('tells a withdrawn consent and offers to withdraw only the consents given', async () => {
    const { wrapper } = await renderTab([
      buildTermsDecision(),
      buildTermsDecision({ userId: 'user-2', accepted: false, withdrawn: true }),
      buildTermsDecision({ userId: 'user-3', documentKind: 'Conditions' }),
    ])

    await vi.waitFor(() => expect(wrapper.findAll('.el-table__body tr')).toHaveLength(3))
    const [given, withdrawn, conditions] = wrapper.findAll('.el-table__body tr')
    const action = t('pages.admin.eventDetail.terms.withdraw.action')
    expect(findButtons(action, given?.element)).toHaveLength(1)
    expect(withdrawn?.text()).toContain(t('pages.admin.eventDetail.terms.withdrawn'))
    expect(findButtons(action, withdrawn?.element)).toHaveLength(0)
    expect(findButtons(action, conditions?.element)).toHaveLength(0)
  })

  it('records a withdrawal once confirmed and lists the decision again', async () => {
    const posted: string[] = []
    server.use(
      http.post(
        '/api/events/:eventId/terms/decisions/:userId/:termsDocumentId/withdraw',
        ({ request }) => {
          posted.push(new URL(request.url).pathname)
          return new HttpResponse(null, { status: 204 })
        },
      ),
    )
    const urls: URL[] = []
    const { wrapper } = await renderTab([buildTermsDecision()], urls)
    await vi.waitFor(() => expect(wrapper.findAll('.el-table__body tr')).toHaveLength(1))

    await click(findButton(t('pages.admin.eventDetail.terms.withdraw.action')))
    expect(messageBox().textContent).toContain(
      t('pages.admin.eventDetail.terms.withdraw.message', {
        document: 'Autorización de imagen',
        name: 'Ada Lovelace',
      }),
    )
    const loads = urls.length
    await acceptMessageBox()

    await expectNotification(t('pages.admin.eventDetail.terms.withdraw.done'))
    expect(posted).toEqual([`/api/events/${EVENT_ID}/terms/decisions/user-1/terms-1/withdraw`])
    expect(urls.length).toBeGreaterThan(loads)
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
