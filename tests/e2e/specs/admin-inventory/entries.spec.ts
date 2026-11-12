import {
  addInventoryEntry,
  createInventoryCategory,
  inventoryColumn,
  inventoryEntries,
  inventoryValues,
} from '../../support/api/inventory'
import { dayText } from '../../support/data/formats'
import { dayFromToday } from '../../support/data/time'
import { uniqueTitle } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { InventoryCategoryPage } from '../../support/pages/admin-inventory/inventory-category-page'
import { InventoryPage } from '../../support/pages/admin-inventory/inventory-page'
import { confirm, notification } from '../../support/ui/element-plus'

const MATERIAL = 'Material'
const LENGTH = 'Largo'
const CHECKED = 'Revisado'
const WORKS = 'Funciona'
const STATE = 'Estado'
const NOTES = 'Notas'
const UNITS = 'Unidades'
const AVAILABLE = 'Disponible'
const LENT = 'Prestado'

test.describe('inventory rows', () => {
  test('a row added through its dialog is stored in canonical form and counted in the totals', async ({
    pageAs,
    admin,
  }) => {
    const category = await createInventoryCategory(admin.api, [
      { name: MATERIAL, type: 'Text', required: true },
      { name: LENGTH, type: 'Number' },
      { name: CHECKED, type: 'Date' },
      { name: WORKS, type: 'Boolean' },
      { name: STATE, type: 'List', options: [AVAILABLE, LENT] },
      { name: UNITS, type: 'Quantity' },
    ])
    const material = uniqueTitle('Portátil')
    const checked = dayFromToday(-3)
    const page = await pageAs(admin)
    const inventory = new InventoryPage(page)
    await inventory.open()
    await inventory.find(category.name)
    await inventory.openCategory(category.name)
    const rows = new InventoryCategoryPage(page)
    await rows.expectOpen(category)

    const dialog = await rows.startAdd()
    await dialog.pickDay(inventoryColumn(category, CHECKED), checked)
    await dialog.textBoxOf(inventoryColumn(category, MATERIAL)).fill(material)
    await dialog.textBoxOf(inventoryColumn(category, LENGTH)).fill('12,5')
    await dialog.choose(inventoryColumn(category, WORKS), 'true')
    await dialog.choose(inventoryColumn(category, STATE), LENT)
    await dialog.textBoxOf(inventoryColumn(category, UNITS)).fill('3')
    await dialog.saveAndClose()

    await expect(
      notification(page, t('pages.admin.inventoryCategory.toasts.created')),
    ).toBeVisible()
    const row = rows.table.row(material)
    await expect(row).toContainText('12,5')
    await expect(row).toContainText(dayText(checked))
    await expect(row).toContainText(t('pages.admin.inventoryCategory.yes'))
    await expect(row).toContainText(LENT)
    await expect(rows.quantityOf(material)).toHaveText('3')
    await expect(rows.summary).toHaveText(
      t('pages.admin.inventoryCategory.summary', { count: 1, total: 3 }),
    )
    const stored = await inventoryEntries(admin.api, category.id)
    expect(stored).toHaveLength(1)
    expect(stored[0]?.quantity).toBe(3)
    expect(stored[0]?.values).toStrictEqual(
      inventoryValues(category, {
        [MATERIAL]: material,
        [LENGTH]: '12.5',
        [CHECKED]: checked,
        [WORKS]: 'true',
        [STATE]: LENT,
      }),
    )
    await inventory.open()
    await inventory.find(category.name)
    await expect(inventory.totalOf(category.name)).toHaveText('3')
  })

  test('the quantity buttons add one to a row and take one from it, keeping its other values', async ({
    pageAs,
    admin,
  }) => {
    const category = await createInventoryCategory(admin.api, [
      { name: MATERIAL, type: 'Text', required: true },
      { name: UNITS, type: 'Quantity' },
    ])
    const material = uniqueTitle('Cable HDMI')
    const entry = await addInventoryEntry(admin.api, category, { [MATERIAL]: material }, 2)
    const page = await pageAs(admin)
    const rows = new InventoryCategoryPage(page)
    await rows.goto(category)

    await rows.step(material, 1)

    await expect(rows.quantityOf(material)).toHaveText('3')
    expect(await inventoryEntries(admin.api, category.id)).toStrictEqual([
      { ...entry, quantity: 3 },
    ])

    await rows.step(material, -1)

    await expect(rows.quantityOf(material)).toHaveText('2')
    await expect(rows.summary).toHaveText(
      t('pages.admin.inventoryCategory.summary', { count: 1, total: 2 }),
    )
    expect(await inventoryEntries(admin.api, category.id)).toStrictEqual([entry])
  })

  test('editing a row replaces its values', async ({ pageAs, admin }) => {
    const category = await createInventoryCategory(admin.api, [
      { name: MATERIAL, type: 'Text', required: true },
      { name: NOTES, type: 'LongText' },
      { name: UNITS, type: 'Quantity' },
    ])
    const before = uniqueTitle('Linterna')
    const after = uniqueTitle('Linterna frontal')
    const entry = await addInventoryEntry(
      admin.api,
      category,
      { [MATERIAL]: before, [NOTES]: 'Revisar las pilas antes del taller' },
      4,
    )
    const page = await pageAs(admin)
    const rows = new InventoryCategoryPage(page)
    await rows.goto(category)
    const material = inventoryColumn(category, MATERIAL)

    await rows.table.rowAction(before, 'edit')
    await expect(rows.dialog.textBoxOf(material)).toHaveValue(before)
    await rows.dialog.textBoxOf(material).fill(after)
    await rows.dialog.textBoxOf(inventoryColumn(category, NOTES)).fill('')
    await rows.dialog.textBoxOf(inventoryColumn(category, UNITS)).fill('6')
    await rows.dialog.saveAndClose()

    await expect(
      notification(page, t('pages.admin.inventoryCategory.toasts.updated')),
    ).toBeVisible()
    await expect(rows.quantityOf(after)).toHaveText('6')
    await expect(rows.table.row(before)).toHaveCount(0)
    expect(await inventoryEntries(admin.api, category.id)).toStrictEqual([
      { id: entry.id, quantity: 6, values: inventoryValues(category, { [MATERIAL]: after }) },
    ])
  })

  test('deleting a row removes only that one from the category', async ({ pageAs, admin }) => {
    const category = await createInventoryCategory(admin.api, [
      { name: MATERIAL, type: 'Text', required: true },
    ])
    const kept = await addInventoryEntry(admin.api, category, { [MATERIAL]: uniqueTitle('Mesa') })
    const removed = uniqueTitle('Silla')
    await addInventoryEntry(admin.api, category, { [MATERIAL]: removed })
    const page = await pageAs(admin)
    const rows = new InventoryCategoryPage(page)
    await rows.goto(category)

    await rows.table.rowAction(removed, 'delete')
    await confirm(page)

    await expect(
      notification(page, t('pages.admin.inventoryCategory.toasts.deleted')),
    ).toBeVisible()
    await expect(rows.table.row(removed)).toHaveCount(0)
    await expect(rows.summary).toHaveText(
      t('pages.admin.inventoryCategory.summary', { count: 1, total: 1 }),
    )
    expect(await inventoryEntries(admin.api, category.id)).toStrictEqual([kept])
  })
})
