import { expect, type Locator, type Page } from '@playwright/test'

import { DataTable } from '../../ui/data-table'
import { AdminShell } from '../admin-shell'
import { FormDialog } from './form-dialog'

/** A catalog of the «Catálogos» page, by the test id of its region without `-section`. */
export type CatalogName = 'event-categories' | 'terms-documents'

/** One catalog of the «Catálogos» page: its region, create button and table. */
export class CatalogRegion {
  /** The region of the catalog, with its heading. */
  readonly root: Locator

  /** The button that opens the create dialog. */
  readonly newButton: Locator

  /** The entries of the catalog, with the search box by name. */
  readonly table: DataTable

  constructor(
    private readonly page: Page,
    name: CatalogName,
  ) {
    this.root = page.getByTestId(`${name}-section`)
    this.newButton = this.root.getByTestId('catalog-section-new')
    this.table = new DataTable(page, this.root)
  }

  /** Opens the create dialog, which carries the test id `testId`, and waits for it. */
  async startCreate(testId: string): Promise<FormDialog> {
    await this.newButton.click()
    const form = new FormDialog(this.page, testId)
    await expect(form.root).toBeVisible()
    return form
  }

  /** Narrows the table to the entries whose name contains `text`. */
  async search(text: string): Promise<void> {
    await this.table.search(text)
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
    this.categories = new CatalogRegion(page, 'event-categories')
    this.terms = new CatalogRegion(page, 'terms-documents')
  }

  /** Opens the page and waits for both catalogs to load. */
  async open(): Promise<void> {
    await this.shell.open('admin-catalogs')
    await this.categories.table.expectSettled()
    await this.terms.table.expectSettled()
  }

  /** The create or edit dialog that carries the test id `testId`. */
  form(testId: string): FormDialog {
    return new FormDialog(this.page, testId)
  }
}
