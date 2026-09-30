import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import TermsDocumentsSection from '@/pages/admin/catalogs/ui/TermsDocumentsSection.vue'
import type { TermsDocumentResponse } from '@/shared/api/generated/models'

import { renderWithProviders, t } from '../../../../../support/render'
import { apiError, http, HttpResponse, paged, server } from '../../../../../support/server'
import { buildTermsDocument, richText } from '../../../../../support/builders'
import {
  acceptMessageBox,
  click,
  dismissDialog,
  expectNotification,
  findButton,
  inputValue,
  isDialogOpen,
  openDialog,
  queryOf,
  typeInto,
} from '../../../../../support/dom'
import { ColumnSearch } from '@/shared/ui/column-filter'
import { RichTextEditor } from '@/shared/ui/rich-text-editor'

const NEW_TITLE = 'pages.admin.catalogs.termsDocuments.form.newHeader'
const EDIT_TITLE = 'pages.admin.catalogs.termsDocuments.form.editHeader'

function serveTerms(items: TermsDocumentResponse[] = [buildTermsDocument()]) {
  const urls: string[] = []
  server.use(
    http.get('/api/events/termsDocument', ({ request }) => {
      urls.push(request.url)
      return HttpResponse.json(paged(items))
    }),
  )
  return urls
}

async function renderSection() {
  return renderWithProviders(TermsDocumentsSection, { attach: true })
}

describe('TermsDocumentsSection', () => {
  it('lists terms documents sorted by name and filters by name', async () => {
    const urls = serveTerms()
    const { wrapper } = await renderSection()

    expect(wrapper.text()).toContain(t('pages.admin.catalogs.termsDocuments.title'))
    expect(wrapper.find('.el-table__body').text()).toContain('Privacy')
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'name' })

    wrapper.findComponent(ColumnSearch).vm.$emit('update:modelValue', 'priv')
    await flushPromises()
    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({ name: 'priv' })
  })

  it('shows empty and error states', async () => {
    serveTerms([])
    const empty = await renderSection()
    expect(empty.wrapper.text()).toContain(t('pages.admin.catalogs.termsDocuments.empty.none'))
    empty.wrapper.unmount()

    server.use(http.get('/api/events/termsDocument', () => apiError(500)))
    const failed = await renderSection()
    await vi.waitFor(() =>
      expect(failed.wrapper.text()).toContain(t('pages.admin.catalogs.termsDocuments.empty.error')),
    )
  })

  it('requires a name and non-blank content before creating a document', async () => {
    serveTerms([])
    let created: unknown
    server.use(
      http.post('/api/events/termsDocument', async ({ request }) => {
        created = await request.json()
        return HttpResponse.json(buildTermsDocument(), { status: 201 })
      }),
    )
    const { wrapper } = await renderSection()

    await click(findButton(t('pages.admin.catalogs.termsDocuments.newButton')))
    const dialog = openDialog(t(NEW_TITLE))
    await click(findButton(t('common.save'), dialog))
    expect(dialog.textContent).toContain(
      t('pages.admin.catalogs.termsDocuments.form.problems.contentRequired'),
    )
    expect(dialog.querySelector('.ca-invalid')).not.toBeNull()
    expect(wrapper.findComponent(RichTextEditor).props('invalid')).toBe(true)
    expect(wrapper.findComponent(RichTextEditor).props('upload')).toBeUndefined()

    await typeInto('#terms-document-name', ' Cookies ')
    wrapper.findComponent(RichTextEditor).vm.$emit('update:modelValue', richText('   '))
    await click(findButton(t('common.save'), dialog))
    expect(created).toBeUndefined()

    wrapper.findComponent(RichTextEditor).vm.$emit('update:modelValue', richText('We use cookies'))
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('pages.admin.catalogs.termsDocuments.toasts.created'))
    expect(created).toEqual({ name: 'Cookies', description: richText('We use cookies') })
    await vi.waitFor(() => expect(isDialogOpen(t(NEW_TITLE))).toBe(false))
  })

  it('edits an existing document', async () => {
    serveTerms()
    let updated: unknown
    server.use(
      http.put('/api/events/termsDocument/:id', async ({ request, params }) => {
        updated = { id: params.id, body: await request.json() }
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await renderSection()

    await click(findButton(t('common.edit')))
    const dialog = openDialog(t(EDIT_TITLE))
    expect(inputValue('#terms-document-name')).toBe('Privacy')
    await typeInto('#terms-document-name', 'Privacy policy')
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('pages.admin.catalogs.termsDocuments.toasts.updated'))
    expect(updated).toEqual({
      id: 'terms-1',
      body: { name: 'Privacy policy', description: richText('Terms body') },
    })
  })

  it('reports create and update failures and closes on cancel', async () => {
    serveTerms([buildTermsDocument()])
    server.use(
      http.post('/api/events/termsDocument', () => apiError(500)),
      http.put('/api/events/termsDocument/:id', () => apiError(500)),
    )
    const { wrapper } = await renderSection()

    await click(findButton(t('pages.admin.catalogs.termsDocuments.newButton')))
    await typeInto('#terms-document-name', 'Cookies')
    wrapper.findComponent(RichTextEditor).vm.$emit('update:modelValue', richText('Body'))
    await click(findButton(t('common.save'), openDialog(t(NEW_TITLE))))
    await vi.waitFor(() =>
      expect(document.body.querySelectorAll('.el-notification--error')).toHaveLength(1),
    )
    await click(findButton(t('common.cancel'), openDialog(t(NEW_TITLE))))
    await vi.waitFor(() => expect(isDialogOpen(t(NEW_TITLE))).toBe(false))

    await click(findButton(t('common.edit')))
    await click(findButton(t('common.save'), openDialog(t(EDIT_TITLE))))
    await vi.waitFor(() =>
      expect(document.body.querySelectorAll('.el-notification--error')).toHaveLength(2),
    )
    await dismissDialog(wrapper, t(EDIT_TITLE))
    await vi.waitFor(() => expect(isDialogOpen(t(EDIT_TITLE))).toBe(false))
  })

  it('deletes documents and reports failures', async () => {
    serveTerms([buildTermsDocument()])
    let fail = false
    const deleted: string[] = []
    server.use(
      http.delete('/api/events/termsDocument/:id', ({ params }) => {
        if (fail) return apiError(500)
        deleted.push(String(params.id))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await renderSection()

    await click(findButton(t('common.delete')))
    await acceptMessageBox()
    await expectNotification(t('pages.admin.catalogs.termsDocuments.toasts.deleted'))
    expect(deleted).toEqual(['terms-1'])

    fail = true
    await click(findButton(t('common.delete')))
    await acceptMessageBox()
    await expectNotification(t('common.error'))
  })
})
