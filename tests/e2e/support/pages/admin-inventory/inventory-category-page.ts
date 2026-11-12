import { expect, type Locator, type Page } from '@playwright/test'
import type { InventoryCategoryResponse } from '@contract/inventoryCategoryResponse'
import type { InventoryColumnResponse } from '@contract/inventoryColumnResponse'

import { inventoryOption } from '../../api/inventory'
import { pickerDate } from '../../data/formats'
import { DataTable } from '../../ui/data-table'
import { chooseOption, fillPicker } from '../../ui/element-plus'
import { FormDialog } from '../admin-content/form-dialog'

/** The dialog that adds a row to an inventory category or edits one, with a field per column. */
export class InventoryEntryDialog extends FormDialog {
  constructor(page: Page) {
    super(page, 'inventory-entry-dialog')
  }

  /** The field of `column`: its label, its control and its message. */
  fieldOf(column: InventoryColumnResponse): Locator {
    return this.root
      .getByTestId('inventory-entry-field')
      .filter({ has: this.root.page().locator(`label[for="inventory-entry-${column.id}"]`) })
  }

  /** The text box of a text, long text, number or quantity `column`. */
  textBoxOf(column: InventoryColumnResponse): Locator {
    return this.fieldOf(column).locator('input, textarea').first()
  }

  /** Types `day` (`YYYY-MM-DD`) into the picker of the date `column`. */
  async pickDay(column: InventoryColumnResponse, day: string): Promise<void> {
    await fillPicker(this.fieldOf(column), pickerDate(day))
  }

  /**
   * Picks `value` in the select of `column`: the name of an option of a list, or `true` or `false`
   * for a yes or no column.
   */
  async choose(column: InventoryColumnResponse, value: string): Promise<void> {
    const option = column.type === 'List' ? inventoryOption(column, value).id : value
    await chooseOption(this.fieldOf(column).locator('.el-select'), option)
  }
}

/**
 * The rows of an inventory category, at `/admin/inventory/{id}`: its name and summary, the table
 * with a column each, the quantity buttons and the dialog that adds or edits a row.
 */
export class InventoryCategoryPage {
  /** The main landmark of the page. */
  readonly main: Locator

  /** The name of the category, in the page header. */
  readonly title: Locator

  /** How many rows the category has and its total, under its name. */
  readonly summary: Locator

  /** The rows of the category. */
  readonly table: DataTable

  /** The headers of the table, one per column of the category and the actions. */
  readonly headers: Locator

  /** The header button that opens the dialog to add a row. */
  readonly newEntry: Locator

  /** The add or edit dialog of a row. */
  readonly dialog: InventoryEntryDialog

  constructor(private readonly page: Page) {
    this.main = page.getByTestId('page-admin-inventory-category')
    this.title = this.main.getByTestId('admin-page-header-title')
    this.summary = this.main.getByTestId('admin-page-header-subtitle')
    this.table = new DataTable(page, this.main)
    this.headers = this.table.root.locator('.el-table__header th')
    this.newEntry = this.main.getByTestId('inventory-entry-new')
    this.dialog = new InventoryEntryDialog(page)
  }

  /** The path of the rows of the category `categoryId`. */
  static path(categoryId: string): string {
    return `/admin/inventory/${categoryId}`
  }

  /** Opens the rows of `category` and waits for them. */
  async goto(category: InventoryCategoryResponse): Promise<void> {
    await this.page.goto(InventoryCategoryPage.path(category.id))
    await this.expectOpen(category)
  }

  /** Expects the page of `category` to be open, with its rows loaded. */
  async expectOpen(category: InventoryCategoryResponse): Promise<void> {
    await expect(this.page).toHaveURL(InventoryCategoryPage.path(category.id))
    await expect(this.title).toHaveText(category.name)
    await this.table.expectSettled()
  }

  /** Opens the dialog to add a row and waits for it. */
  async startAdd(): Promise<InventoryEntryDialog> {
    await this.newEntry.click()
    await expect(this.dialog.root).toBeVisible()
    return this.dialog
  }

  /** The quantity shown in the row that contains `text`. */
  quantityOf(text: string): Locator {
    return this.table.row(text).getByTestId('inventory-quantity-value')
  }

  /** Adds one to the quantity of the row that contains `text` (`1`) or takes one from it (`-1`). */
  async step(text: string, by: -1 | 1): Promise<void> {
    const button = by === 1 ? 'inventory-quantity-increase' : 'inventory-quantity-decrease'
    await this.table.row(text).getByTestId(button).click()
  }
}
