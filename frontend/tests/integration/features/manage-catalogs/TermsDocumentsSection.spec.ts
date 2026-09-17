import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { TermsDocumentsSection } from '@/features/manage-catalogs'
import type { TermsDocumentResponse } from '@/shared/api/generated/models'
import { ColumnSearch } from '@/shared/ui'
import RichTextEditor from '@/shared/ui/RichTextEditor.vue'

import {
  buildTermsDocument,
  richText,
  without,
} from '../../../support/fixtures/admin-content/builders'
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
} from '../../../support/fixtures/admin-content/helpers'
import { renderWithProviders, t } from '../../../support/render'
import { apiError, http, HttpResponse, paged, server } from '../../../support/server'

const NEW_TITLE = 'features.manageCatalogs.terms.newHeader'
const EDIT_TITLE = 'features.manageCatalogs.terms.editHeader'

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

    expect(wrapper.text()).toContain(t('features.manageCatalogs.terms.title'))
    expect(wrapper.find('.el-table__body').text()).toContain('Privacy')
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'name' })

    wrapper.findComponent(ColumnSearch).vm.$emit('update:modelValue', 'priv')
    await flushPromises()
    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({ name: 'priv' })
  })

  it('shows empty and error states', async () => {
    serveTerms([])
    const empty = await renderSection()
    expect(empty.wrapper.text()).toContain(t('features.manageCatalogs.terms.empty'))
    empty.wrapper.unmount()

    server.use(http.get('/api/events/termsDocument', () => apiError(500)))
    const failed = await renderSection()
    await vi.waitFor(() =>
      expect(failed.wrapper.text()).toContain(t('features.manageCatalogs.terms.loadError')),
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

    await click(findButton(t('features.manageCatalogs.terms.newButton')))
    const dialog = openDialog(t(NEW_TITLE))
    await click(findButton(t('common.save'), dialog))
    expect(dialog.textContent).toContain(t('features.manageCatalogs.terms.contentRequired'))
    expect(dialog.querySelector('.ca-invalid')).not.toBeNull()

    await typeInto('#terms-document-name', ' Cookies ')
    wrapper.findComponent(RichTextEditor).vm.$emit('update:modelValue', richText('   '))
    await click(findButton(t('common.save'), dialog))
    expect(created).toBeUndefined()

    wrapper.findComponent(RichTextEditor).vm.$emit('update:modelValue', richText('We use cookies'))
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('features.manageCatalogs.terms.created'))
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

    await expectNotification(t('features.manageCatalogs.terms.updated'))
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

    await click(findButton(t('features.manageCatalogs.terms.newButton')))
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

  it('deletes documents, reports failures and ignores rows without id', async () => {
    const ghost = without(buildTermsDocument({ name: 'Ghost' }), 'id')
    serveTerms([buildTermsDocument(), ghost])
    let fail = false
    const deleted: string[] = []
    server.use(
      http.delete('/api/events/termsDocument/:id', ({ params }) => {
        if (fail) return apiError(500)
        deleted.push(String(params.id))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { wrapper } = await renderSection()
    const rows = () => wrapper.findAll('.el-table__body tr')

    await click(findButton(t('common.delete'), rows()[1]?.element ?? document.body))
    await acceptMessageBox()
    await flushPromises()
    expect(document.body.querySelectorAll('.el-notification')).toHaveLength(0)

    await click(findButton(t('common.delete'), rows()[0]?.element ?? document.body))
    await acceptMessageBox()
    await expectNotification(t('features.manageCatalogs.terms.deleted'))
    expect(deleted).toEqual(['terms-1'])

    fail = true
    await click(findButton(t('common.delete'), rows()[0]?.element ?? document.body))
    await acceptMessageBox()
    await expectNotification(t('common.error'))
  })
})
