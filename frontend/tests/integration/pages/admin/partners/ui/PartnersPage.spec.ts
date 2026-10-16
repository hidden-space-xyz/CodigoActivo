import { flushPromises } from '@vue/test-utils'
import { ElDatePicker, ElInputNumber, ElPagination, ElTable } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { elementPlusLocale } from '@/app/config/element-plus-locale'
import { PartnersPage } from '@/pages/admin/partners'
import PartnerFormDialog from '@/pages/admin/partners/ui/PartnerFormDialog.vue'
import type { PartnerResponse } from '@/shared/api/generated/models'
import { formatDate } from '@/shared/lib/date'

import { setMediaQueryMatches } from '../../../../../support/media'
import { renderApp, renderWithProviders, t } from '../../../../../support/render'
import { apiError, http, HttpResponse, paged, server } from '../../../../../support/server'
import { buildPartnerResponse } from '../../../../../support/builders'
import {
  acceptMessageBox,
  cancelMessageBox,
  click,
  dismissDialog,
  expectNotification,
  findButton,
  isDialogOpen,
  openDialog,
  pickFiles,
  queryOf,
  tp,
  typeInto,
} from '../../../../../support/dom'
import { ColumnFilterDate, ColumnSearch } from '@/shared/ui/column-filter'
import { SearchInput } from '@/shared/ui/search-input'

function servePartners(items: PartnerResponse[] = [buildPartnerResponse()]) {
  const urls: string[] = []
  server.use(
    http.get('/api/partners', ({ request }) => {
      urls.push(request.url)
      return HttpResponse.json(paged(items))
    }),
    http.get('/api/files/:id', ({ params }) =>
      HttpResponse.json({ id: String(params.id), name: 'logo.png', extension: 'png' }),
    ),
  )
  return urls
}

async function renderPage() {
  return renderWithProviders(PartnersPage, { attach: true })
}

function sortLabel(column: string): string {
  return elementPlusLocale.el.table.sortLabel.replace('{column}', column)
}

describe('admin partners page', () => {
  it('lists partners sorted by tier with logos, website links and formatted dates', async () => {
    const urls = servePartners([
      buildPartnerResponse(),
      buildPartnerResponse({ id: 'partner-2', name: 'Globex', website: null, fromDate: '' }),
    ])

    const { wrapper } = await renderPage()

    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'tier' })
    expect(wrapper.find('.data-table__count').text()).toBe(tp('table.results', 2))
    expect(wrapper.find('.data-table__footer').isVisible()).toBe(false)
    expect(
      wrapper.findAll('th .caret-wrapper').map((caret) => caret.attributes('aria-label')),
    ).toEqual(
      [
        t('common.name'),
        t('pages.admin.partners.columns.tier'),
        t('pages.admin.partners.columns.website'),
        t('pages.admin.partners.columns.fromDate'),
      ].map(sortLabel),
    )

    const rows = wrapper.findAll('.el-table__body tr')
    expect(rows).toHaveLength(2)
    const [first, second] = rows
    if (!first || !second) throw new Error('missing rows')
    const title = first.find('.table-primary-cell__title')
    expect(title.text()).toBe('Acme')
    expect(title.attributes('title')).toBe('Acme')
    const logo = first.find('.table-primary-cell__leading .list-thumb')
    expect(logo.classes()).toEqual(
      expect.arrayContaining(['list-thumb--table', 'list-thumb--contain']),
    )
    expect(logo.find('img').attributes()).toMatchObject({
      src: '/api/files/thumb-partner-1/content',
      alt: '',
    })
    const link = first.find('a')
    expect(link.attributes()).toMatchObject({
      href: 'https://acme.test',
      target: '_blank',
      rel: 'noopener',
    })
    expect(link.find('.external-link__text').text()).toBe('acme.test')
    expect(first.text()).toContain(formatDate('2024-03-15'))
    expect(
      first.findAll('.row-actions button').map((button) => button.attributes('aria-label')),
    ).toEqual([t('common.edit'), t('common.delete')])
    expect(findButton(t('common.delete'), first.element).classList).toContain(
      'row-actions__button--danger',
    )
    expect(first.find('.row-actions__more').exists()).toBe(false)
    expect(second.find('.table-primary-cell__title').text()).toBe('Globex')
    expect(second.find('a').exists()).toBe(false)
    expect(second.text()).toContain('—')
  })

  it('pins the name column on wide screens only and the actions column on every screen', async () => {
    servePartners()

    const wide = await renderPage()
    const wideRow = wide.wrapper.find('.el-table__body tr')
    expect(wideRow.find('td.el-table-fixed-column--left .table-primary-cell').exists()).toBe(true)
    expect(wideRow.find('td.el-table-fixed-column--right .row-actions').exists()).toBe(true)
    wide.wrapper.unmount()

    setMediaQueryMatches((query) => query === '(max-width: 640px)')
    const narrow = await renderPage()
    const narrowRow = narrow.wrapper.find('.el-table__body tr')
    expect(narrowRow.find('td.el-table-fixed-column--left').exists()).toBe(false)
    expect(narrowRow.find('td.el-table-fixed-column--right .row-actions').exists()).toBe(true)
  })

  it('renders through the application router for admins', async () => {
    servePartners()

    const { wrapper } = await renderApp('/admin/partners', { user: { isAdmin: true } })

    await vi.waitFor(() => expect(wrapper.text()).toContain('Acme'))
    expect(wrapper.text()).toContain(t('pages.admin.partners.header.title'))
  })

  it('shows the empty message when there are no partners', async () => {
    servePartners([])

    const { wrapper } = await renderPage()

    expect(wrapper.text()).toContain(t('pages.admin.partners.empty.none'))
  })

  it('shows a load error when the partners request fails', async () => {
    server.use(http.get('/api/partners', () => apiError(500)))

    const { wrapper } = await renderPage()

    await vi.waitFor(() => expect(wrapper.text()).toContain(t('pages.admin.partners.empty.error')))
  })

  it('sends the toolbar search, column filters, sorting and pagination to the API', async () => {
    const urls = servePartners([buildPartnerResponse()])
    const { wrapper } = await renderPage()

    const search = wrapper.findComponent(SearchInput)
    expect(search.props('label')).toBe(t('pages.admin.partners.search.name'))
    search.vm.$emit('update:modelValue', 'acme')
    const searches = wrapper.findAllComponents(ColumnSearch)
    expect(searches.map((columnSearch) => columnSearch.props('label'))).toEqual([
      t('pages.admin.partners.columns.tier'),
      t('pages.admin.partners.columns.website'),
    ])
    searches[0]?.vm.$emit('update:modelValue', 2)
    searches[1]?.vm.$emit('update:modelValue', 'acme.test')
    wrapper
      .findComponent(ColumnFilterDate)
      .vm.$emit('update:modelValue', [new Date(2024, 0, 1), new Date(2024, 11, 31)])
    await flushPromises()
    wrapper.findComponent(ElTable).vm.$emit('sort-change', { prop: 'name', order: 'descending' })
    await flushPromises()

    expect(queryOf(urls.at(-1) ?? '')).toEqual({
      page: '1',
      pageSize: '25',
      name: 'acme',
      tier: '2',
      website: 'acme.test',
      fromDateFrom: '2024-01-01',
      fromDateTo: '2024-12-31',
      sort: '-name',
    })

    const pagination = wrapper.findComponent(ElPagination)
    pagination.vm.$emit('size-change', 50)
    await flushPromises()
    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({ page: '1', pageSize: '50' })
    pagination.vm.$emit('current-change', 3)
    await flushPromises()
    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({ page: '3', pageSize: '50' })
  })

  it('shows a chip per header filter and clears every filter at once', async () => {
    const urls = servePartners([buildPartnerResponse()])
    const { wrapper } = await renderPage()

    wrapper.findComponent(SearchInput).vm.$emit('update:modelValue', 'acme')
    const [tier, website] = wrapper.findAllComponents(ColumnSearch)
    tier?.vm.$emit('update:modelValue', 2)
    tier?.vm.$emit('apply')
    website?.vm.$emit('update:modelValue', 'acme.test')
    website?.vm.$emit('apply')
    await flushPromises()

    expect(wrapper.findAll('.filter-chip__label').map((chip) => chip.text())).toEqual([
      t('table.filterChip', { label: t('pages.admin.partners.columns.tier'), value: '2' }),
      t('table.filterChip', {
        label: t('pages.admin.partners.columns.website'),
        value: 'acme.test',
      }),
    ])

    await click(findButton(t('table.clearFilters')))

    expect(queryOf(urls.at(-1) ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'tier' })
    expect(wrapper.find('.filter-chip').exists()).toBe(false)
    expect(wrapper.find<HTMLInputElement>('.data-table__search input').element.value).toBe('')
  })

  it('creates a partner after uploading its logo', async () => {
    servePartners([])
    let uploaded: File | undefined
    let created: unknown
    server.use(
      http.post('/api/files', async ({ request }) => {
        const form = await request.formData()
        uploaded = form.get('file') as File
        return HttpResponse.json({ id: 'new-thumb' })
      }),
      http.post('/api/partners', async ({ request }) => {
        created = await request.json()
        return HttpResponse.json(buildPartnerResponse({ id: 'partner-9' }), { status: 201 })
      }),
    )
    const { wrapper } = await renderPage()

    await click(findButton(t('pages.admin.partners.newPartner')))
    const dialog = openDialog(t('pages.admin.partners.form.newHeader'))
    await typeInto('#partner-name', '  Initech  ')
    await typeInto('#partner-website', ' https://initech.test ')
    wrapper
      .findComponent(PartnerFormDialog)
      .findComponent(ElDatePicker)
      .vm.$emit('update:modelValue', new Date(2025, 5, 7))
    wrapper.findComponent(ElInputNumber).vm.$emit('update:modelValue', 3)
    const fileInput = dialog.querySelector<HTMLInputElement>('input[type="file"]')
    if (!fileInput) throw new Error('missing file input')
    await pickFiles(fileInput, [new File(['png'], 'logo.png', { type: 'image/png' })])

    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('pages.admin.partners.toasts.created'))
    expect(uploaded).toBeInstanceOf(File)
    expect(uploaded?.name).toBe('logo.png')
    expect(created).toEqual({
      name: 'Initech',
      tier: 3,
      website: 'https://initech.test',
      fromDate: '2025-06-07',
      thumbnailId: 'new-thumb',
    })
    await vi.waitFor(() =>
      expect(isDialogOpen(t('pages.admin.partners.form.newHeader'))).toBe(false),
    )
  })

  it('closes the partner dialog with its close button', async () => {
    servePartners([])
    const { wrapper } = await renderPage()

    await click(findButton(t('pages.admin.partners.newPartner')))
    await dismissDialog(wrapper, t('pages.admin.partners.form.newHeader'))

    await vi.waitFor(() =>
      expect(isDialogOpen(t('pages.admin.partners.form.newHeader'))).toBe(false),
    )
  })

  it('updates an existing partner keeping its logo', async () => {
    servePartners([buildPartnerResponse()])
    let updated: unknown
    server.use(
      http.put('/api/partners/:id', async ({ request, params }) => {
        updated = { id: params.id, body: await request.json() }
        return HttpResponse.json(buildPartnerResponse())
      }),
    )
    await renderPage()

    await click(findButton(t('common.edit')))
    const dialog = openDialog(t('pages.admin.partners.form.editHeader'))
    await typeInto('#partner-name', 'Acme Corp')
    await typeInto('#partner-website', '   ')
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('pages.admin.partners.toasts.updated'))
    expect(updated).toEqual({
      id: 'partner-1',
      body: {
        name: 'Acme Corp',
        tier: 1,
        website: null,
        fromDate: '2024-03-15',
        thumbnailId: 'thumb-partner-1',
      },
    })
  })

  it('shows an error toast with the trace id when saving fails', async () => {
    servePartners([buildPartnerResponse()])
    server.use(http.put('/api/partners/:id', () => apiError(409)))
    await renderPage()

    await click(findButton(t('common.edit')))
    const dialog = openDialog(t('pages.admin.partners.form.editHeader'))
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('table.ref', { id: 'trace-123' }))
    expect(isDialogOpen(t('pages.admin.partners.form.editHeader'))).toBe(true)
  })

  it('shows an error toast when creating fails', async () => {
    servePartners([])
    server.use(
      http.post('/api/files', () => HttpResponse.json({ id: 'new-thumb' })),
      http.post('/api/partners', () => apiError(500)),
    )
    const { wrapper } = await renderPage()

    await click(findButton(t('pages.admin.partners.newPartner')))
    const dialog = openDialog(t('pages.admin.partners.form.newHeader'))
    await typeInto('#partner-name', 'Initech')
    wrapper
      .findComponent(PartnerFormDialog)
      .findComponent(ElDatePicker)
      .vm.$emit('update:modelValue', new Date(2025, 0, 1))
    const fileInput = dialog.querySelector<HTMLInputElement>('input[type="file"]')
    if (!fileInput) throw new Error('missing file input')
    await pickFiles(fileInput, [new File(['png'], 'logo.png', { type: 'image/png' })])
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('common.error'))
    expect(isDialogOpen(t('pages.admin.partners.form.newHeader'))).toBe(true)
  })

  it('deletes a partner after confirmation', async () => {
    servePartners([buildPartnerResponse()])
    const deleted: string[] = []
    server.use(
      http.delete('/api/partners/:id', ({ params }) => {
        deleted.push(String(params.id))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await renderPage()

    await click(findButton(t('common.delete')))
    expect(document.body.querySelector('.el-message-box')?.textContent).toContain('"Acme"')
    await acceptMessageBox()

    await expectNotification(t('pages.admin.partners.toasts.deleted'))
    expect(deleted).toEqual(['partner-1'])
  })

  it('does not delete when the confirmation is cancelled', async () => {
    servePartners([buildPartnerResponse()])
    const deleted: string[] = []
    server.use(
      http.delete('/api/partners/:id', ({ params }) => {
        deleted.push(String(params.id))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await renderPage()

    await click(findButton(t('common.delete')))
    await cancelMessageBox()
    await flushPromises()

    expect(deleted).toEqual([])
  })

  it('shows an error toast when deleting fails', async () => {
    servePartners([buildPartnerResponse()])
    server.use(http.delete('/api/partners/:id', () => apiError(500)))
    await renderPage()

    await click(findButton(t('common.delete')))
    await acceptMessageBox()

    await expectNotification(t('common.error'))
  })
})
