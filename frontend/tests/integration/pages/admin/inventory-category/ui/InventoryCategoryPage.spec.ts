import { ElDatePicker, ElInputNumber, ElSelect, ElTable } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { InventoryCategoryPage } from '@/pages/admin/inventory-category'
import InventoryEntryDialog from '@/pages/admin/inventory-category/ui/InventoryEntryDialog.vue'
import type {
  InventoryCategoryResponse,
  InventoryEntryResponse,
} from '@/shared/api/generated/models'
import { ColumnFilterSelect, ColumnSearch } from '@/shared/ui/column-filter'

import { renderWithProviders, t } from '../../../../../support/render'
import { apiError, http, HttpResponse, noContent, server } from '../../../../../support/server'
import {
  buildInventoryCategoryResponse,
  buildInventoryEntryResponse,
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
  typeInto,
} from '../../../../../support/dom'

const ENTRIES: InventoryEntryResponse[] = [
  buildInventoryEntryResponse(),
  buildInventoryEntryResponse({ id: 'entry-2', quantity: 12, values: { size: 'size-m' } }),
]

function serveCategory(
  category: InventoryCategoryResponse = buildInventoryCategoryResponse(),
  entries: InventoryEntryResponse[] = ENTRIES,
) {
  server.use(
    http.get('/api/inventory/categories/category-1', () => HttpResponse.json(category)),
    http.get('/api/inventory/categories/category-1/entries', () => HttpResponse.json(entries)),
  )
}

async function renderPage() {
  const rendered = await renderWithProviders(InventoryCategoryPage, {
    props: { categoryId: 'category-1' },
    attach: true,
  })
  await vi.waitFor(() => expect(rendered.wrapper.findAll('.el-table__body tr')).not.toHaveLength(0))
  return rendered
}

function rowTexts(wrapper: Awaited<ReturnType<typeof renderPage>>['wrapper']): string[][] {
  return wrapper.findAll('.el-table__body tr').map((row) =>
    row
      .findAll('td')
      .slice(0, -1)
      .map((cell) => cell.text()),
  )
}

describe('admin inventory category page', () => {
  it('shows the rows with a column each, sorted by the first one', async () => {
    serveCategory()

    const { wrapper } = await renderPage()

    expect(wrapper.find('h1').text()).toBe('Camisetas')
    expect(wrapper.text()).toContain('2 filas · total 42')
    expect(wrapper.findAll('thead th').map((cell) => cell.text().trim())).toEqual(
      expect.arrayContaining(['Talla', 'Color', 'Cantidad']),
    )
    expect(rowTexts(wrapper)).toEqual([
      ['S', 'Negra', '30'],
      ['M', '—', '12'],
    ])
    expect(wrapper.findAll('.el-table__body tr')[0]?.find('.color-tag').text()).toBe('S')
  })

  it('filters and sorts the rows in the browser', async () => {
    serveCategory()
    const { wrapper } = await renderPage()

    wrapper.findComponent(ColumnFilterSelect).vm.$emit('update:modelValue', 'size-m')
    wrapper.findComponent(ColumnFilterSelect).vm.$emit('apply')
    await vi.waitFor(() => expect(rowTexts(wrapper)).toEqual([['M', '—', '12']]))

    wrapper.findComponent(ColumnFilterSelect).vm.$emit('update:modelValue', null)
    wrapper.findComponent(ColumnFilterSelect).vm.$emit('apply')
    wrapper.findComponent(ElTable).vm.$emit('sort-change', { prop: 'quantity', order: 'ascending' })
    await vi.waitFor(() => expect(rowTexts(wrapper).map((row) => row[2])).toEqual(['12', '30']))

    const [color] = wrapper.findAllComponents(ColumnSearch)
    color?.vm.$emit('update:modelValue', 'negra')
    color?.vm.$emit('apply')
    await vi.waitFor(() => expect(rowTexts(wrapper)).toEqual([['S', 'Negra', '30']]))
  })

  it('adds one to a quantity keeping the other values', async () => {
    serveCategory()
    let sent: unknown
    server.use(
      http.put('/api/inventory/categories/category-1/entries/entry-1', async ({ request }) => {
        sent = await request.json()
        return HttpResponse.json(buildInventoryEntryResponse({ quantity: 31 }))
      }),
    )
    await renderPage()

    const [increase] = findButtons(t('pages.admin.inventoryCategory.increase'))
    if (!increase) throw new Error('missing step button')
    await click(increase)

    await vi.waitFor(() =>
      expect(sent).toEqual({ quantity: 31, values: { size: 'size-s', color: 'Negra' } }),
    )
  })

  it('adds a row through a field for each column', async () => {
    serveCategory()
    let created: unknown
    server.use(
      http.post('/api/inventory/categories/category-1/entries', async ({ request }) => {
        created = await request.json()
        return HttpResponse.json(buildInventoryEntryResponse(), { status: 201 })
      }),
    )
    const { wrapper } = await renderPage()

    await click(findButton(t('pages.admin.inventoryCategory.newEntry')))
    const dialog = openDialog(t('pages.admin.inventoryCategory.form.newHeader'))
    const form = wrapper.findComponent(InventoryEntryDialog)
    form.findComponent(ElSelect).vm.$emit('update:modelValue', 'size-m')
    await typeInto('#inventory-entry-color', ' Blanca ')
    form.findComponent(ElInputNumber).vm.$emit('update:modelValue', 7)
    await click(findButton(t('common.save'), dialog))

    await expectNotification(t('pages.admin.inventoryCategory.toasts.created'))
    expect(created).toEqual({ quantity: 7, values: { size: 'size-m', color: 'Blanca' } })
  })

  it('shows what is missing instead of saving a row without its required values', async () => {
    serveCategory()
    await renderPage()

    await click(findButton(t('pages.admin.inventoryCategory.newEntry')))
    const dialog = openDialog(t('pages.admin.inventoryCategory.form.newHeader'))
    await click(findButton(t('common.save'), dialog))

    expect(dialog.querySelectorAll('[data-testid="inventory-entry-field-error"]')).toHaveLength(2)
    expect(isDialogOpen(t('pages.admin.inventoryCategory.form.newHeader'))).toBe(true)
  })

  it('edits and deletes a row', async () => {
    serveCategory()
    let updated: unknown
    const deleted: string[] = []
    server.use(
      http.put('/api/inventory/categories/category-1/entries/entry-1', async ({ request }) => {
        updated = await request.json()
        return HttpResponse.json(buildInventoryEntryResponse())
      }),
      http.delete('/api/inventory/categories/category-1/entries/:id', ({ params }) => {
        deleted.push(String(params.id))
        return noContent()
      }),
    )
    await renderPage()

    const [edit] = findButtons(t('common.edit'))
    if (!edit) throw new Error('missing edit button')
    await click(edit)
    const dialog = openDialog(t('pages.admin.inventoryCategory.form.editHeader'))
    await typeInto('#inventory-entry-color', '')
    await click(findButton(t('common.save'), dialog))
    await expectNotification(t('pages.admin.inventoryCategory.toasts.updated'))
    expect(updated).toEqual({ quantity: 30, values: { size: 'size-s' } })

    const [remove] = findButtons(t('common.delete'))
    if (!remove) throw new Error('missing delete button')
    await click(remove)
    expect(messageBox().textContent).toContain('"S"')
    await acceptMessageBox()
    await expectNotification(t('pages.admin.inventoryCategory.toasts.deleted'))
    expect(deleted).toEqual(['entry-1'])
  })

  it('offers a field for every type of column', async () => {
    serveCategory(
      buildInventoryCategoryResponse({
        columns: [
          { id: 'notes', name: 'Notas', type: 'LongText', required: false, options: [] },
          { id: 'ram', name: 'Memoria', type: 'Number', required: false, options: [] },
          { id: 'bought', name: 'Compra', type: 'Date', required: false, options: [] },
          { id: 'works', name: 'Funciona', type: 'Boolean', required: false, options: [] },
        ],
        entryCount: 0,
        total: 0,
      }),
      [
        buildInventoryEntryResponse({
          quantity: null,
          values: { notes: 'Una nota', works: 'true' },
        }),
      ],
    )
    let created: unknown
    server.use(
      http.post('/api/inventory/categories/category-1/entries', async ({ request }) => {
        created = await request.json()
        return HttpResponse.json(buildInventoryEntryResponse(), { status: 201 })
      }),
    )
    const { wrapper } = await renderPage()
    expect(rowTexts(wrapper)).toEqual([['Una nota', '—', '—', 'Sí']])

    await click(findButton(t('pages.admin.inventoryCategory.newEntry')))
    const dialog = openDialog(t('pages.admin.inventoryCategory.form.newHeader'))
    const form = wrapper.findComponent(InventoryEntryDialog)
    expect(dialog.querySelector('textarea#inventory-entry-notes')).not.toBeNull()
    await typeInto('#inventory-entry-ram', '8,5')
    form.findComponent(ElDatePicker).vm.$emit('update:modelValue', '2024-03-05')
    form.findComponent(ElSelect).vm.$emit('update:modelValue', 'false')
    await click(findButton(t('common.save'), dialog))

    await vi.waitFor(() =>
      expect(created).toEqual({
        quantity: null,
        values: { ram: '8.5', bought: '2024-03-05', works: 'false' },
      }),
    )
  })

  it('tells when the category does not exist', async () => {
    server.use(
      http.get('/api/inventory/categories/category-1', () =>
        apiError(404, 'InventoryCategoryNotFound'),
      ),
    )

    const { wrapper } = await renderWithProviders(InventoryCategoryPage, {
      props: { categoryId: 'category-1' },
    })

    await vi.waitFor(() =>
      expect(wrapper.text()).toContain(t('pages.admin.inventoryCategory.notFound')),
    )
  })
})
