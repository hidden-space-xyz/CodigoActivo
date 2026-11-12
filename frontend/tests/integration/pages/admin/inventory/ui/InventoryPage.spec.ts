import { ElCheckbox, ElSelect } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { InventoryPage } from '@/pages/admin/inventory'
import InventoryCategoryDialog from '@/pages/admin/inventory/ui/InventoryCategoryDialog.vue'
import type { InventoryCategorySummaryResponse } from '@/shared/api/generated/models'

import { renderApp, renderWithProviders, t } from '../../../../../support/render'
import {
  apiError,
  http,
  HttpResponse,
  noContent,
  paged,
  server,
} from '../../../../../support/server'
import {
  buildInventoryCategoryResponse,
  buildInventoryEntryResponse,
  buildInventorySummaryResponse,
} from '../../../../../support/builders'
import {
  acceptMessageBox,
  click,
  expectNotification,
  findButton,
  findButtons,
  isDialogOpen,
  messageBox,
  openDialog,
  queryOf,
  typeInto,
} from '../../../../../support/dom'

const NEW_HEADER = 'pages.admin.inventory.form.newHeader'
const EDIT_HEADER = 'pages.admin.inventory.form.editHeader'

function serveCategories(
  items: InventoryCategorySummaryResponse[] = [buildInventorySummaryResponse()],
) {
  const urls: string[] = []
  server.use(
    http.get('/api/inventory/categories', ({ request }) => {
      urls.push(request.url)
      return HttpResponse.json(paged(items))
    }),
  )
  return urls
}

function serveEditedCategory() {
  server.use(
    http.get('/api/inventory/categories/category-1', () =>
      HttpResponse.json(buildInventoryCategoryResponse()),
    ),
    http.get('/api/inventory/categories/category-1/entries', () =>
      HttpResponse.json([
        buildInventoryEntryResponse(),
        buildInventoryEntryResponse({ id: 'entry-2', quantity: 12, values: { size: 'size-m' } }),
      ]),
    ),
  )
}

async function renderPage() {
  return renderWithProviders(InventoryPage, { attach: true })
}

function inputsOf(dialog: HTMLElement, testid: string): HTMLInputElement[] {
  return [...dialog.querySelectorAll<HTMLInputElement>(`input[data-testid="${testid}"]`)]
}

async function type(input: HTMLInputElement | undefined, value: string): Promise<void> {
  if (!input) throw new Error('missing input')
  await typeInto(`#${input.id}`, value)
}

describe('admin inventory page', () => {
  it('lists the categories by name with their rows, totals and actions', async () => {
    const urls = serveCategories([
      buildInventorySummaryResponse(),
      buildInventorySummaryResponse({
        id: 'category-2',
        name: 'Portátiles',
        entryCount: 1500,
        total: 1500,
      }),
    ])

    const { wrapper } = await renderPage()

    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'name' })
    const rows = wrapper.findAll('.el-table__body tr')
    expect(rows.map((row) => row.find('.table-primary-cell__title').text())).toEqual([
      'Camisetas',
      'Portátiles',
    ])
    expect(rows[0]?.find('a').attributes('href')).toBe('/admin/inventory/category-1')
    expect(
      wrapper.findAll('[data-testid="admin-inventory-total"]').map((cell) => cell.text()),
    ).toEqual(['42', '1500'])
    expect(
      rows[0]?.findAll('.row-actions button').map((button) => button.attributes('aria-label')),
    ).toEqual([t('pages.admin.inventory.open'), t('common.edit'), t('common.delete')])
  })

  it('renders through the application router for admins', async () => {
    serveCategories([])

    const { wrapper } = await renderApp('/admin/inventory', {
      user: { staffRole: 'Administrator' },
    })

    expect(wrapper.text()).toContain(t('pages.admin.inventory.empty.none'))
  })

  it('opens the rows of a category from its actions', async () => {
    serveCategories()
    const { router } = await renderPage()

    await click(findButton(t('pages.admin.inventory.open')))

    await vi.waitFor(() =>
      expect(router.currentRoute.value.fullPath).toBe('/admin/inventory/category-1'),
    )
  })

  it('creates a category with a list, a quantity and the colors of its options', async () => {
    serveCategories([])
    let created: unknown
    server.use(
      http.post('/api/inventory/categories', async ({ request }) => {
        created = await request.json()
        return HttpResponse.json(buildInventoryCategoryResponse(), { status: 201 })
      }),
    )
    const { wrapper } = await renderPage()

    await click(findButton(t('pages.admin.inventory.newCategory')))
    const dialog = openDialog(t(NEW_HEADER))
    await typeInto('#inventory-category-name', ' Camisetas ')
    await type(inputsOf(dialog, 'inventory-column-name')[0], 'Talla')
    const editor = wrapper.findComponent(InventoryCategoryDialog)
    editor.findAllComponents(ElSelect)[0]?.vm.$emit('update:modelValue', 'List')
    await vi.waitFor(() => expect(inputsOf(dialog, 'inventory-option-name')).toHaveLength(1))
    await type(inputsOf(dialog, 'inventory-option-name')[0], 'S')
    await click(findButton(t('pages.admin.inventory.form.addOption'), dialog))
    await type(inputsOf(dialog, 'inventory-option-name')[1], 'M')
    await click(findButton(t('pages.admin.inventory.form.addColumn'), dialog))
    await type(inputsOf(dialog, 'inventory-column-name')[1], 'Cantidad')
    editor.findAllComponents(ElSelect).at(-1)?.vm.$emit('update:modelValue', 'Quantity')
    editor.findAllComponents(ElCheckbox).at(-1)?.vm.$emit('update:modelValue', true)

    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('pages.admin.inventory.toasts.created'))
    expect(created).toEqual({
      name: 'Camisetas',
      columns: [
        {
          id: null,
          name: 'Talla',
          type: 'List',
          required: false,
          options: [
            { id: null, name: 'S', color: '#6366F1' },
            { id: null, name: 'M', color: '#6366F1' },
          ],
        },
        { id: null, name: 'Cantidad', type: 'Quantity', required: true, options: [] },
      ],
    })
    await vi.waitFor(() => expect(isDialogOpen(t(NEW_HEADER))).toBe(false))
  })

  it('shows what is wrong instead of saving a category without names', async () => {
    serveCategories([])
    await renderPage()

    await click(findButton(t('pages.admin.inventory.newCategory')))
    const dialog = openDialog(t(NEW_HEADER))
    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('pages.admin.inventory.form.problems.nameRequired'))
    expect(dialog.textContent).toContain(
      t('pages.admin.inventory.form.problems.columnNameRequired'),
    )
    expect(isDialogOpen(t(NEW_HEADER))).toBe(true)
  })

  it('edits a category locking what its rows already use', async () => {
    serveCategories()
    serveEditedCategory()
    let updated: unknown
    server.use(
      http.put('/api/inventory/categories/category-1', async ({ request }) => {
        updated = await request.json()
        return HttpResponse.json(buildInventoryCategoryResponse())
      }),
    )
    const { wrapper } = await renderPage()

    await click(findButton(t('common.edit')))
    const dialog = await vi.waitFor(() => openDialog(t(EDIT_HEADER)))
    const editor = wrapper.findComponent(InventoryCategoryDialog)
    const selects = editor.findAllComponents(ElSelect)
    expect(selects.map((select) => select.props('disabled'))).toEqual([true, true, true])
    expect(dialog.textContent).toContain(t('pages.admin.inventory.form.typeLocked'))
    const removeOption = findButtons(t('pages.admin.inventory.form.optionInUse'), dialog)
    expect(removeOption.map((button) => button.disabled)).toEqual([true, true])
    const checkboxes = editor.findAllComponents(ElCheckbox)
    expect(checkboxes.map((checkbox) => checkbox.props('disabled'))).toEqual([false, true, false])
    expect(dialog.textContent).toContain(t('pages.admin.inventory.form.requiredLocked'))

    await type(inputsOf(dialog, 'inventory-column-name')[1], 'Color principal')
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('pages.admin.inventory.toasts.updated'))
    expect(updated).toMatchObject({
      name: 'Camisetas',
      columns: [
        { id: 'size', options: [{ id: 'size-s' }, { id: 'size-m' }] },
        { id: 'color', name: 'Color principal' },
        { id: 'quantity', type: 'Quantity', required: true },
      ],
    })
  })

  it('asks before removing a column whose rows hold values', async () => {
    serveCategories()
    serveEditedCategory()
    await renderPage()

    await click(findButton(t('common.edit')))
    const dialog = await vi.waitFor(() => openDialog(t(EDIT_HEADER)))
    const [, removeColor] = dialog.querySelectorAll<HTMLButtonElement>(
      '[data-testid="inventory-column-remove"]',
    )
    if (!removeColor) throw new Error('missing remove button')
    await click(removeColor)

    expect(messageBox().textContent).toContain(
      'La columna "Color" tiene datos en 1 fila, que se borrarán al guardar.',
    )
    await acceptMessageBox()
    expect(inputsOf(dialog, 'inventory-column-name')).toHaveLength(2)
  })

  it('reports a category that no longer exists instead of opening it', async () => {
    serveCategories()
    server.use(
      http.get('/api/inventory/categories/category-1', () =>
        apiError(404, 'InventoryCategoryNotFound'),
      ),
      http.get('/api/inventory/categories/category-1/entries', () =>
        apiError(404, 'InventoryCategoryNotFound'),
      ),
    )
    await renderPage()

    await click(findButton(t('common.edit')))

    await expectNotification(t('errors.InventoryCategoryNotFound'))
    expect(isDialogOpen(t(EDIT_HEADER))).toBe(false)
  })

  it('deletes a category after confirmation', async () => {
    serveCategories()
    const deleted: string[] = []
    server.use(
      http.delete('/api/inventory/categories/:id', ({ params }) => {
        deleted.push(String(params.id))
        return noContent()
      }),
    )
    await renderPage()

    await click(findButton(t('common.delete')))
    expect(messageBox().textContent).toContain('Camisetas')
    await acceptMessageBox()

    await expectNotification(t('pages.admin.inventory.toasts.deleted'))
    expect(deleted).toEqual(['category-1'])
  })
})
