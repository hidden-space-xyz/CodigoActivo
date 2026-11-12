import { expect, type Locator, type Page } from '@playwright/test'

import type { InventoryColumnDefinition } from '../../api/inventory'
import { DataTable } from '../../ui/data-table'
import { chooseOption, setChecked } from '../../ui/element-plus'
import { FormDialog } from '../admin-content/form-dialog'
import { AdminShell } from '../admin-shell'

/**
 * The dialog that creates an inventory category or redefines its columns: the name, then one block
 * per column with its name, type, «Obligatoria» box and, for a list, its options.
 */
export class InventoryCategoryDialog extends FormDialog {
  /** The text box that holds what the category is called. */
  readonly name: Locator

  /** The blocks of the columns, in order. */
  readonly columns: Locator

  /** The button that adds a text column at the end. */
  readonly addColumnButton: Locator

  constructor(page: Page) {
    super(page, 'inventory-category-dialog')
    this.name = this.field('inventory-category-name')
    this.columns = this.field('inventory-column')
    this.addColumnButton = this.field('inventory-column-add')
  }

  /** The block of the column at `index`. */
  column(index: number): Locator {
    return this.columns.nth(index)
  }

  /** The name of the column at `index`. */
  columnName(index: number): Locator {
    return this.column(index).getByTestId('inventory-column-name')
  }

  /** The button that removes the option at `option` of the column at `column`. */
  removeOptionButton(column: number, option: number): Locator {
    return this.column(column).getByTestId('inventory-option-remove').nth(option)
  }

  /** Clicks the button that removes the column at `index`. */
  async removeColumn(index: number): Promise<void> {
    await this.column(index).getByTestId('inventory-column-remove').click()
  }

  /**
   * Sets the column at `index`, still a new one, to `definition`: its name, its type, its
   * «Obligatoria» box and, for a list, the names of its options, the first of which the dialog adds
   * when the type becomes a list.
   */
  async defineColumn(index: number, definition: InventoryColumnDefinition): Promise<void> {
    const column = this.column(index)
    await this.columnName(index).fill(definition.name)
    await chooseOption(column.getByTestId('inventory-column-type'), definition.type)
    await setChecked(column.getByTestId('inventory-column-required'), definition.required ?? false)
    for (const [position, option] of (definition.options ?? []).entries()) {
      if (position > 0) await column.getByTestId('inventory-option-add').click()
      await column.getByTestId('inventory-option-name').nth(position).fill(option)
    }
  }

  /** Adds a column at the end and sets it to `definition`. */
  async addColumn(definition: InventoryColumnDefinition): Promise<void> {
    const index = await this.columns.count()
    await this.addColumnButton.click()
    await expect(this.columns).toHaveCount(index + 1)
    await this.defineColumn(index, definition)
  }
}

/**
 * The inventory, at `/admin/inventory`: the categories by name with their rows and totals, their
 * actions and the dialog that creates or redefines them.
 */
export class InventoryPage {
  /** The administration layout around the page. */
  readonly shell: AdminShell

  /** The categories, with the search box by name. */
  readonly table: DataTable

  /** The header button that opens the dialog to create a category. */
  readonly newCategory: Locator

  /** The create or edit dialog of a category. */
  readonly dialog: InventoryCategoryDialog

  constructor(page: Page) {
    this.shell = new AdminShell(page)
    this.table = new DataTable(page)
    this.newCategory = this.shell.main.getByTestId('admin-inventory-new')
    this.dialog = new InventoryCategoryDialog(page)
  }

  /** Opens the page and waits for the categories to load. */
  async open(): Promise<void> {
    await this.shell.open('admin-inventory')
    await this.table.expectSettled()
  }

  /** Narrows the table to the categories whose name contains `name`. */
  async find(name: string): Promise<void> {
    await this.table.search(name)
  }

  /** Opens the dialog to create a category and waits for it. */
  async startCreate(): Promise<InventoryCategoryDialog> {
    await this.newCategory.click()
    await expect(this.dialog.root).toBeVisible()
    return this.dialog
  }

  /** Opens the dialog of the listed category `name`, filled with its columns, and waits for it. */
  async startEdit(name: string): Promise<InventoryCategoryDialog> {
    await this.table.rowAction(name, 'edit')
    await expect(this.dialog.root).toBeVisible()
    await expect(this.dialog.name).toHaveValue(name)
    return this.dialog
  }

  /** The total of the listed category `name`. */
  totalOf(name: string): Locator {
    return this.table.row(name).getByTestId('admin-inventory-total')
  }

  /** Follows the link of the listed category `name` to its rows. */
  async openCategory(name: string): Promise<void> {
    await this.table.row(name).getByTestId('admin-inventory-category-link').click()
  }
}
