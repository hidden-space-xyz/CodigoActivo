import { flushPromises } from '@vue/test-utils'
import { ElDatePicker, ElInputNumber, ElPagination, ElTable } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { PartnerFormDialog } from '@/features/manage-partners'
import { PartnersPage } from '@/pages/admin/partners'
import type { PartnerResponse } from '@/shared/api/generated/models'
import { ColumnFilterDate, ColumnSearch } from '@/shared/ui'

import { buildPartner, without } from '../../../../support/fixtures/admin-content/builders'
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
  typeInto,
} from '../../../../support/fixtures/admin-content/helpers'
import { renderApp, renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'

function servePartners(items: PartnerResponse[] = [buildPartner()]) {
  const urls: string[] = []
  server.use(
    http.get('/api/partners', ({ request }) => {
      urls.push(request.url)
      return HttpResponse.json(paged(items))
    }),
    http.get('/api/files/:id', ({ params }) =>
      HttpResponse.json({ id: String(params.id), name: 'logo', extension: '.png' }),
    ),
  )
  return urls
}

async function renderPage() {
  return renderWithProviders(PartnersPage, { attach: true })
}

describe('admin partners page', () => {
  it('lists partners sorted by tier with website links and formatted dates', async () => {
    const urls = servePartners([
      buildPartner(),
      buildPartner({ id: 'partner-2', name: 'Globex', website: null, fromDate: '' }),
    ])

    const { wrapper } = await renderPage()

    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'tier' })
    const rows = wrapper.findAll('.el-table__body tr')
    expect(rows).toHaveLength(2)
    expect(rows[0]?.text()).toContain('Acme')
    expect(rows[0]?.find('a').attributes('href')).toBe('https://acme.test')
    expect(rows[0]?.find('img').attributes('src')).toBe('/api/files/thumb-partner-1/content')
    expect(rows[1]?.text()).toContain('Globex')
    expect(rows[1]?.find('a').exists()).toBe(false)
    expect(rows[1]?.text()).toContain('—')
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

  it('sends column filters, sorting and pagination to the API', async () => {
    const urls = servePartners([buildPartner()])
    const { wrapper } = await renderPage()

    const searches = wrapper.findAllComponents(ColumnSearch)
    searches[0]?.vm.$emit('update:modelValue', 'acme')
    searches[0]?.vm.$emit('apply')
    searches[1]?.vm.$emit('update:modelValue', 2)
    searches[2]?.vm.$emit('update:modelValue', 'acme.test')
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
        return HttpResponse.json(buildPartner({ id: 'partner-9' }), { status: 201 })
      }),
    )
    const { wrapper } = await renderPage()

    await click(findButton(t('pages.admin.partners.newPartner')))
    const dialog = openDialog(t('features.managePartners.newHeader'))
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
    await vi.waitFor(() => expect(isDialogOpen(t('features.managePartners.newHeader'))).toBe(false))
  })

  it('closes the partner dialog with its close button', async () => {
    servePartners([])
    const { wrapper } = await renderPage()

    await click(findButton(t('pages.admin.partners.newPartner')))
    await dismissDialog(wrapper, t('features.managePartners.newHeader'))

    await vi.waitFor(() => expect(isDialogOpen(t('features.managePartners.newHeader'))).toBe(false))
  })

  it('updates an existing partner keeping its logo', async () => {
    servePartners([buildPartner()])
    let updated: unknown
    server.use(
      http.put('/api/partners/:id', async ({ request, params }) => {
        updated = { id: params.id, body: await request.json() }
        return HttpResponse.json(buildPartner())
      }),
    )
    await renderPage()

    await click(findButton(t('common.edit')))
    const dialog = openDialog(t('features.managePartners.editHeader'))
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
    servePartners([buildPartner()])
    server.use(http.put('/api/partners/:id', () => apiError(409)))
    await renderPage()

    await click(findButton(t('common.edit')))
    const dialog = openDialog(t('features.managePartners.editHeader'))
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('table.ref', { id: 'trace-123' }))
    expect(isDialogOpen(t('features.managePartners.editHeader'))).toBe(true)
  })

  it('shows an error toast when creating fails', async () => {
    servePartners([])
    server.use(
      http.post('/api/files', () => HttpResponse.json({ id: 'new-thumb' })),
      http.post('/api/partners', () => apiError(500)),
    )
    const { wrapper } = await renderPage()

    await click(findButton(t('pages.admin.partners.newPartner')))
    const dialog = openDialog(t('features.managePartners.newHeader'))
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
    expect(isDialogOpen(t('features.managePartners.newHeader'))).toBe(true)
  })

  it('deletes a partner after confirmation', async () => {
    servePartners([buildPartner()])
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
    servePartners([buildPartner()])
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
    servePartners([buildPartner()])
    server.use(http.delete('/api/partners/:id', () => apiError(500)))
    await renderPage()

    await click(findButton(t('common.delete')))
    await acceptMessageBox()

    await expectNotification(t('common.error'))
  })

  it('ignores deletion of a row without id', async () => {
    const ghost = without(buildPartner({ name: 'Ghost' }), 'id')
    servePartners([ghost])
    await renderPage()

    await click(findButton(t('common.delete')))
    await acceptMessageBox()
    await flushPromises()

    expect(document.body.querySelectorAll('.el-notification')).toHaveLength(0)
  })
})
