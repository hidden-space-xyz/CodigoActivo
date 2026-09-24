import { flushPromises } from '@vue/test-utils'
import { ElButton, ElSelect } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { ResourceFormDialog } from '@/features/manage-resources'
import { ResourcesPage } from '@/pages/admin/resources'
import type { ResourceListItemResponse } from '@/shared/api/generated/models'
import { ColumnFilterDate, ColumnFilterSelect, ColumnSearch } from '@/shared/ui'

import {
  buildResource,
  externalType,
  internalType,
  without,
} from '../../../../support/fixtures/admin-content/builders'
import {
  acceptMessageBox,
  click,
  dismissDialog,
  expectNotification,
  findButton,
  findButtons,
  isDialogOpen,
  openDialog,
  pickFiles,
  queryOf,
  typeInto,
} from '../../../../support/fixtures/admin-content/helpers'
import { renderApp, renderWithProviders, t } from '../../../../support/render'
import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'

function listItem(overrides: ResourceListItemResponse = {}): ResourceListItemResponse {
  return { ...without(buildResource(), 'description'), ...overrides }
}

function serveResources(items: ResourceListItemResponse[] = [listItem()]) {
  const urls: string[] = []
  server.use(
    http.get('/api/resources', ({ request }) => {
      urls.push(request.url)
      return HttpResponse.json(paged(items))
    }),
    http.get('/api/resources/types', () => HttpResponse.json([internalType, externalType])),
    http.get('/api/files/:id', () => HttpResponse.json({ name: 'cover', extension: '.png' })),
  )
  return urls
}

/** Detail handler that leaves `/api/resources/types` (same route shape) to the catalog response. */
function detail(resolver: () => Response | Promise<Response>) {
  return http.get('/api/resources/:id', ({ params }) =>
    params.id === 'types' ? HttpResponse.json([internalType, externalType]) : resolver(),
  )
}

async function renderPage() {
  return renderWithProviders(ResourcesPage, { attach: true })
}

describe('admin resources page', () => {
  it('lists resources newest first with type, url and creation date', async () => {
    const urls = serveResources([
      listItem({ url: 'https://vuejs.org', type: externalType }),
      listItem({ id: 'resource-2', title: 'Untyped', subtitle: '', url: null, type: {} }),
    ])

    const { wrapper } = await renderPage()

    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: '-createdAt' })
    const rows = wrapper.findAll('.el-table__body tr')
    expect(rows).toHaveLength(2)
    expect(rows[0]?.text()).toContain('Vue guide')
    expect(rows[0]?.text()).toContain('Enlace')
    expect(rows[0]?.find('a').attributes('href')).toBe('https://vuejs.org')
    expect(rows[0]?.text()).toMatch(/2025/)
    expect(rows[1]?.find('a').exists()).toBe(false)
    expect(rows[1]?.text()).toContain('—')
  })

  it('renders through the application router for admins', async () => {
    serveResources()

    const { wrapper } = await renderApp('/admin/resources', { user: { isAdmin: true } })

    await vi.waitFor(() => expect(wrapper.text()).toContain('Vue guide'))
  })

  it('shows empty and error states', async () => {
    serveResources([])
    const empty = await renderPage()
    expect(empty.wrapper.text()).toContain(t('pages.admin.resources.empty.none'))
    empty.wrapper.unmount()

    server.use(http.get('/api/resources', () => apiError(500)))
    const failed = await renderPage()
    await vi.waitFor(() =>
      expect(failed.wrapper.text()).toContain(t('pages.admin.resources.empty.error')),
    )
  })

  it('sends title, subtitle, type, url and creation filters', async () => {
    const urls = serveResources()
    const { wrapper } = await renderPage()

    const searches = wrapper.findAllComponents(ColumnSearch)
    searches[0]?.vm.$emit('update:modelValue', 'vue')
    searches[1]?.vm.$emit('update:modelValue', 'start')
    searches[2]?.vm.$emit('update:modelValue', 'vuejs.org')
    wrapper.findComponent(ColumnFilterSelect).vm.$emit('update:modelValue', 'type-link')
    wrapper
      .findComponent(ColumnFilterDate)
      .vm.$emit('update:modelValue', [new Date(2025, 0, 1), null])
    await flushPromises()

    expect(queryOf(urls.at(-1) ?? '')).toEqual({
      page: '1',
      pageSize: '25',
      sort: '-createdAt',
      title: 'vue',
      subtitle: 'start',
      url: 'vuejs.org',
      resourceTypeId: 'type-link',
      createdFrom: '2025-01-01',
    })
    expect(wrapper.findComponent(ColumnFilterSelect).props('options')).toEqual([
      { label: 'Artículo', value: 'type-article' },
      { label: 'Enlace', value: 'type-link' },
    ])
  })

  it('creates an external resource', async () => {
    serveResources([])
    let created: unknown
    server.use(
      http.post('/api/files', () => HttpResponse.json({ id: 'new-thumb' })),
      http.post('/api/resources', async ({ request }) => {
        created = await request.json()
        return HttpResponse.json(buildResource(), { status: 201 })
      }),
    )
    const { wrapper } = await renderPage()

    await click(findButton(t('pages.admin.resources.newResource')))
    const dialog = openDialog(t('features.manageResources.newHeader'))
    await typeInto('#resource-title', 'Vue')
    await typeInto('#resource-subtitle', 'Docs')
    wrapper
      .findComponent(ResourceFormDialog)
      .findComponent(ElSelect)
      .vm.$emit('update:modelValue', 'type-link')
    await flushPromises()
    await typeInto('#resource-url', 'https://vuejs.org')
    const input = dialog.querySelector<HTMLInputElement>('.thumb input[type="file"]')
    if (!input) throw new Error('missing file input')
    await pickFiles(input, [new File(['x'], 'cover.png', { type: 'image/png' })])
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('pages.admin.resources.toasts.created'))
    expect(created).toEqual({
      title: 'Vue',
      subtitle: 'Docs',
      description: null,
      url: 'https://vuejs.org',
      resourceTypeId: 'type-link',
      thumbnailId: 'new-thumb',
    })
    await vi.waitFor(() =>
      expect(isDialogOpen(t('features.manageResources.newHeader'))).toBe(false),
    )
  })

  it('closes the resource dialog with its close button', async () => {
    serveResources([])
    const { wrapper } = await renderPage()

    await click(findButton(t('pages.admin.resources.newResource')))
    await dismissDialog(wrapper, t('features.manageResources.newHeader'))

    await vi.waitFor(() =>
      expect(isDialogOpen(t('features.manageResources.newHeader'))).toBe(false),
    )
  })

  it('shows an error toast when creating fails', async () => {
    serveResources([])
    server.use(
      http.post('/api/files', () => HttpResponse.json({ id: 'new-thumb' })),
      http.post('/api/resources', () => apiError(500)),
    )
    const { wrapper } = await renderPage()

    await click(findButton(t('pages.admin.resources.newResource')))
    const dialog = openDialog(t('features.manageResources.newHeader'))
    await typeInto('#resource-title', 'Vue')
    await typeInto('#resource-subtitle', 'Docs')
    wrapper
      .findComponent(ResourceFormDialog)
      .findComponent(ElSelect)
      .vm.$emit('update:modelValue', 'type-link')
    await flushPromises()
    await typeInto('#resource-url', 'https://vuejs.org')
    const input = dialog.querySelector<HTMLInputElement>('.thumb input[type="file"]')
    if (!input) throw new Error('missing file input')
    await pickFiles(input, [new File(['x'], 'cover.png', { type: 'image/png' })])
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('common.error'))
  })

  it('loads the full resource before editing and saves the changes', async () => {
    serveResources()
    let updated: unknown
    server.use(
      detail(() => HttpResponse.json(buildResource())),
      http.put('/api/resources/:id', async ({ request, params }) => {
        updated = { id: params.id, body: await request.json() }
        return HttpResponse.json(buildResource())
      }),
    )
    await renderPage()

    await click(findButton(t('common.edit')))
    const dialog = openDialog(t('features.manageResources.editHeader'))
    await typeInto('#resource-title', 'Vue guide v2')
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('pages.admin.resources.toasts.updated'))
    expect(updated).toMatchObject({
      id: 'resource-1',
      body: { title: 'Vue guide v2', resourceTypeId: 'type-article' },
    })
  })

  it('shows an error toast when updating fails', async () => {
    serveResources()
    server.use(
      detail(() => HttpResponse.json(buildResource())),
      http.put('/api/resources/:id', () => apiError(500)),
    )
    await renderPage()

    await click(findButton(t('common.edit')))
    await click(findButton(t('common.save'), openDialog(t('features.manageResources.editHeader'))))

    await expectNotification(t('common.error'))
  })

  it('does not open the editor for a resource that no longer exists', async () => {
    serveResources()
    server.use(detail(() => apiError(404)))
    await renderPage()

    await click(findButton(t('common.edit')))

    await vi.waitFor(() =>
      expect(document.body.querySelector('.el-notification--error')).not.toBeNull(),
    )
    expect(isDialogOpen(t('features.manageResources.editHeader'))).toBe(false)
  })

  it('reports a resource that no longer exists with the not-found message', async () => {
    serveResources()
    server.use(detail(() => apiError(404)))
    await renderPage()

    await click(findButton(t('common.edit')))

    await expectNotification(t('pages.admin.resources.toasts.notFound'))
  })

  it('reports a failure loading the resource detail', async () => {
    serveResources()
    server.use(detail(() => apiError(500)))
    await renderPage()

    await click(findButton(t('common.edit')))

    await expectNotification(t('common.error'))
  })

  it('ignores new edit or create requests while a detail is loading', async () => {
    serveResources()
    let release: () => void = () => undefined
    let detailCalls = 0
    server.use(
      detail(async () => {
        detailCalls += 1
        await new Promise<void>((resolve) => {
          release = resolve
        })
        return HttpResponse.json(buildResource())
      }),
    )
    const { wrapper } = await renderPage()

    await click(findButton(t('common.edit')))
    await vi.waitFor(() => expect(detailCalls).toBe(1))
    expect(findButton(t('pages.admin.resources.newResource')).disabled).toBe(true)

    // Disabled buttons swallow clicks; emit directly to exercise the handlers' own guards.
    const buttons = wrapper.findAllComponents(ElButton)
    const create = buttons.find(
      (button) => button.text() === t('pages.admin.resources.newResource'),
    )
    const edit = buttons.find((button) => button.attributes('aria-label') === t('common.edit'))
    create?.vm.$emit('click', new MouseEvent('click'))
    edit?.vm.$emit('click', new MouseEvent('click'))
    await flushPromises()

    expect(detailCalls).toBe(1)
    expect(isDialogOpen(t('features.manageResources.newHeader'))).toBe(false)

    release()
    await vi.waitFor(() =>
      expect(isDialogOpen(t('features.manageResources.editHeader'))).toBe(true),
    )
  })

  it('ignores editing a row without id', async () => {
    const ghost = without(listItem(), 'id')
    serveResources([ghost])
    await renderPage()

    await click(findButton(t('common.edit')))

    expect(isDialogOpen(t('features.manageResources.editHeader'))).toBe(false)
  })

  it('deletes a resource after confirmation and reports failures', async () => {
    serveResources()
    const deleted: string[] = []
    let fail = false
    server.use(
      http.delete('/api/resources/:id', ({ params }) => {
        if (fail) return apiError(500)
        deleted.push(String(params.id))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    await renderPage()

    await click(findButton(t('common.delete')))
    expect(document.body.querySelector('.el-message-box')?.textContent).toContain('"Vue guide"')
    await acceptMessageBox()
    await expectNotification(t('pages.admin.resources.toasts.deleted'))
    expect(deleted).toEqual(['resource-1'])

    fail = true
    await click(findButtons(t('common.delete'))[0] ?? findButton(t('common.delete')))
    await acceptMessageBox()
    await expectNotification(t('common.error'))
  })

  it('ignores deleting a row without id', async () => {
    const ghost = without(listItem(), 'id')
    serveResources([ghost])
    await renderPage()

    await click(findButton(t('common.delete')))
    await acceptMessageBox()
    await flushPromises()

    expect(document.body.querySelectorAll('.el-notification')).toHaveLength(0)
  })
})
