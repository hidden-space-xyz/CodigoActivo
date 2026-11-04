import { expect, type Locator, type Page } from '@playwright/test'

import { DataTable } from '../../ui/data-table'
import { AdminShell } from '../admin-shell'
import { FormDialog } from './form-dialog'

/** A catalog of the «Catálogos» page. */
export type CatalogName = 'Categorías de eventos' | 'Términos y condiciones'

/** One catalog of the «Catálogos» page: its region, create button and table. */
export class CatalogRegion {
  /** The region of the catalog, named by its heading. */
  readonly root: Locator

  /** The button that opens the create dialog. */
  readonly newButton: Locator

  /** The entries of the catalog, with the search box by name. */
  readonly table: DataTable

  constructor(
    private readonly page: Page,
    name: CatalogName,
    newLabel: string,
  ) {
    this.root = page.getByRole('region', { name, exact: true })
    this.newButton = this.root.getByRole('button', { name: newLabel, exact: true })
    this.table = new DataTable(page, this.root)
  }

  /** Opens the create dialog, titled `title`, and waits for it. */
  async startCreate(title: string): Promise<FormDialog> {
    await this.newButton.click()
    const form = new FormDialog(this.page, title)
    await expect(form.root).toBeVisible()
    return form
  }

  /** Narrows the table to the entries whose name contains `text`. */
  async search(text: string): Promise<void> {
    await this.table.search('Buscar por nombre', text)
  }
}

/** The «Catálogos» page: the event categories and the terms documents events ask to accept. */
export class CatalogsPage {
  /** The administration layout around the page. */
  readonly shell: AdminShell

  /** The event categories. */
  readonly categories: CatalogRegion

  /** The terms and conditions and consents. */
  readonly terms: CatalogRegion

  constructor(private readonly page: Page) {
    this.shell = new AdminShell(page)
    this.categories = new CatalogRegion(page, 'Categorías de eventos', 'Nueva categoría')
    this.terms = new CatalogRegion(page, 'Términos y condiciones', 'Nuevos términos')
  }

  /** Opens the page and waits for both catalogs to load. */
  async open(): Promise<void> {
    await this.shell.open('Catálogos')
    await this.categories.table.expectSettled()
    await this.terms.table.expectSettled()
  }

  /** The create or edit dialog titled `title`. */
  form(title: string): FormDialog {
    return new FormDialog(this.page, title)
  }
}
