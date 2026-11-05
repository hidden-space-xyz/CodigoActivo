import { expect, type Locator, type Page } from '@playwright/test'

import { DataTable } from '../../ui/data-table'
import { AdminShell, type AdminSection } from '../admin-shell'
import { FormDialog } from './form-dialog'

/** An admin page that lists one kind of content in a table and edits it in dialogs. */
export type ContentSection = Extract<
  AdminSection,
  'admin-news' | 'admin-partners' | 'admin-resources'
>

/**
 * The news, partner or resource administration: the page header with its create button, the table
 * with its toolbar search and column filters, and the create and edit dialogs.
 */
export class ContentAdminPage {
  /** The administration layout around the page. */
  readonly shell: AdminShell

  /** The list of the content, with its search box and column filters. */
  readonly table: DataTable

  /** The header button that opens the create dialog. */
  readonly newButton: Locator

  constructor(
    private readonly page: Page,
    private readonly section: ContentSection,
  ) {
    this.shell = new AdminShell(page)
    this.table = new DataTable(page)
    this.newButton = this.shell.main.getByTestId(`${section}-new`)
  }

  /** Opens the page and waits for its table to load. */
  async open(): Promise<void> {
    await this.shell.open(this.section)
    await this.table.expectSettled()
  }

  /** The create or edit dialog that carries the test id `testId`. */
  form(testId: string): FormDialog {
    return new FormDialog(this.page, testId)
  }

  /** Opens the create dialog, which carries the test id `testId`, and waits for it. */
  async startCreate(testId: string): Promise<FormDialog> {
    await this.newButton.click()
    const form = this.form(testId)
    await expect(form.root).toBeVisible()
    return form
  }

  /** Narrows the table with the toolbar search box. */
  async search(text: string): Promise<void> {
    await this.table.search(text)
  }
}
