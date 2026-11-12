import {
  addInventoryEntry,
  createInventoryCategory,
  inventoryCategoriesNamed,
  inventoryCategoryById,
  inventoryCategoryNamed,
  inventoryEntries,
  inventoryValues,
  type InventoryColumnDefinition,
} from '../../support/api/inventory'
import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { InventoryCategoryPage } from '../../support/pages/admin-inventory/inventory-category-page'
import { InventoryPage } from '../../support/pages/admin-inventory/inventory-page'
import { confirm, messageBox, notification } from '../../support/ui/element-plus'

const MATERIAL = 'Material'
const STATE = 'Estado'
const SITUATION = 'Situación'
const NOTES = 'Notas'
const UNITS = 'Unidades'
const AVAILABLE = 'Disponible'
const LENT = 'Prestado'

test.describe('inventory categories', () => {
  test('an administrator creates a category whose columns the API keeps in order', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const inventory = new InventoryPage(page)
    await inventory.open()
    const name = uniqueTitle('Inventario')
    const material = { name: MATERIAL, type: 'Text', required: true, options: [] } as const
    const state = {
      name: STATE,
      type: 'List',
      required: false,
      options: [AVAILABLE, LENT],
    } as const
    const units = { name: UNITS, type: 'Quantity', required: false, options: [] } as const

    const dialog = await inventory.startCreate()
    await dialog.name.fill(name)
    await dialog.defineColumn(0, material)
    await dialog.addColumn(state)
    await dialog.addColumn(units)
    await dialog.saveAndClose()

    await expect(notification(page, t('pages.admin.inventory.toasts.created'))).toBeVisible()
    await inventory.find(name)
    await expect(inventory.totalOf(name)).toHaveText('0')
    const stored = await inventoryCategoryNamed(admin.api, name)
    expect(
      stored.columns.map((column): InventoryColumnDefinition => ({
        name: column.name,
        type: column.type,
        required: column.required,
        options: column.options.map((option) => option.name),
      })),
    ).toStrictEqual([material, state, units])
  })

  test('a second category with the same name is refused', async ({ pageAs, admin }) => {
    const existing = await createInventoryCategory(admin.api, [{ name: MATERIAL, type: 'Text' }])
    const page = await pageAs(admin)
    const inventory = new InventoryPage(page)
    await inventory.open()

    const dialog = await inventory.startCreate()
    await dialog.name.fill(existing.name)
    await dialog.defineColumn(0, { name: MATERIAL, type: 'Text' })
    await dialog.save()

    await expect(notification(page, t('errors.InventoryCategoryNameAlreadyExists'))).toBeVisible()
    await expect(dialog.root).toBeVisible()
    const named = await inventoryCategoriesNamed(admin.api, existing.name)
    expect(named.map((category) => category.id)).toStrictEqual([existing.id])
  })

  test('redefining a category keeps what its rows hold and lets go only of unused options', async ({
    pageAs,
    admin,
  }) => {
    const category = await createInventoryCategory(admin.api, [
      { name: MATERIAL, type: 'Text', required: true },
      { name: STATE, type: 'List', options: [AVAILABLE, LENT] },
    ])
    const material = uniqueTitle('Proyector')
    const entry = await addInventoryEntry(admin.api, category, {
      [MATERIAL]: material,
      [STATE]: AVAILABLE,
    })
    const page = await pageAs(admin)
    const inventory = new InventoryPage(page)
    await inventory.open()
    await inventory.find(category.name)

    const dialog = await inventory.startEdit(category.name)
    await expect(dialog.removeOptionButton(1, 0)).toBeDisabled()
    await dialog.removeOptionButton(1, 1).click()
    await dialog.columnName(1).fill(SITUATION)
    await dialog.saveAndClose()

    await expect(notification(page, t('pages.admin.inventory.toasts.updated'))).toBeVisible()
    const stored = await inventoryCategoryById(admin.api, category.id)
    expect(stored.columns[1]?.id).toBe(category.columns[1]?.id)
    expect(stored.columns[1]?.name).toBe(SITUATION)
    expect(stored.columns[1]?.options.map((option) => option.name)).toStrictEqual([AVAILABLE])
    expect(await inventoryEntries(admin.api, category.id)).toStrictEqual([entry])
    await inventory.openCategory(category.name)
    const rows = new InventoryCategoryPage(page)
    await rows.expectOpen(category)
    await expect(rows.headers).toContainText([MATERIAL, SITUATION])
    await expect(rows.table.row(material)).toContainText(AVAILABLE)
  })

  test('removing a column that holds values asks first and clears them from the rows', async ({
    pageAs,
    admin,
  }) => {
    const category = await createInventoryCategory(admin.api, [
      { name: MATERIAL, type: 'Text', required: true },
      { name: NOTES, type: 'Text' },
    ])
    const material = uniqueTitle('Altavoz')
    await addInventoryEntry(admin.api, category, {
      [MATERIAL]: material,
      [NOTES]: 'Falta el cable de corriente',
    })
    const page = await pageAs(admin)
    const inventory = new InventoryPage(page)
    await inventory.open()
    await inventory.find(category.name)

    const dialog = await inventory.startEdit(category.name)
    await dialog.removeColumn(1)
    await expect(messageBox(page)).toContainText(
      t('pages.admin.inventory.form.removeColumnConfirm.message', { name: NOTES, count: 1 }),
    )
    await confirm(page)
    await expect(dialog.columns).toHaveCount(1)
    await dialog.saveAndClose()

    await expect(notification(page, t('pages.admin.inventory.toasts.updated'))).toBeVisible()
    const stored = await inventoryCategoryById(admin.api, category.id)
    expect(stored.columns.map((column) => column.name)).toStrictEqual([MATERIAL])
    const [entry] = await inventoryEntries(admin.api, category.id)
    expect(entry?.values).toStrictEqual(inventoryValues(category, { [MATERIAL]: material }))
  })

  test('deleting a category removes it with its rows', async ({ pageAs, admin }) => {
    const category = await createInventoryCategory(admin.api, [{ name: MATERIAL, type: 'Text' }])
    await addInventoryEntry(admin.api, category, { [MATERIAL]: uniqueTitle('Mesa plegable') })
    const page = await pageAs(admin)
    const inventory = new InventoryPage(page)
    await inventory.open()
    await inventory.find(category.name)

    await inventory.table.rowAction(category.name, 'delete')
    await confirm(page)

    await expect(notification(page, t('pages.admin.inventory.toasts.deleted'))).toBeVisible()
    await expect(inventory.table.row(category.name)).toHaveCount(0)
    await expect(inventoryCategoryById(admin.api, category.id)).rejects.toThrow(
      'InventoryCategoryNotFound',
    )
    await expect(inventoryEntries(admin.api, category.id)).rejects.toThrow(
      'InventoryCategoryNotFound',
    )
  })
})
