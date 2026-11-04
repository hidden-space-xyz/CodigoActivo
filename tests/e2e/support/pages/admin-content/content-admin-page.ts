import { expect, type Locator, type Page } from '@playwright/test'

import { DataTable } from '../../ui/data-table'
import { AdminShell, type AdminSection } from '../admin-shell'
import { FormDialog } from './form-dialog'

/** An admin page that lists one kind of content in a table and edits it in dialogs. */
export type ContentSection = Extract<AdminSection, 'Novedades' | 'Patrocinadores' | 'Recursos'>

const PAGES: Readonly<Record<ContentSection, { newLabel: string; searchLabel: string }>> = {
  Novedades: { newLabel: 'Añadir novedad', searchLabel: 'Buscar por título o subtítulo' },
  Patrocinadores: { newLabel: 'Nuevo patrocinador', searchLabel: 'Buscar por nombre' },
  Recursos: { newLabel: 'Nuevo recurso', searchLabel: 'Buscar por título o subtítulo' },
}

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

  private readonly searchLabel: string

  constructor(
    private readonly page: Page,
    private readonly section: ContentSection,
  ) {
    const { newLabel, searchLabel } = PAGES[section]
    this.shell = new AdminShell(page)
    this.table = new DataTable(page)
    this.newButton = this.shell.main.getByRole('button', { name: newLabel, exact: true })
    this.searchLabel = searchLabel
  }

  /** Opens the page and waits for its table to load. */
  async open(): Promise<void> {
    await this.shell.open(this.section)
    await this.table.expectSettled()
  }

  /** The create or edit dialog titled `title`. */
  form(title: string): FormDialog {
    return new FormDialog(this.page, title)
  }

  /** Opens the create dialog, titled `title`, and waits for it. */
  async startCreate(title: string): Promise<FormDialog> {
    await this.newButton.click()
    const form = this.form(title)
    await expect(form.root).toBeVisible()
    return form
  }

  /** Narrows the table with the toolbar search box. */
  async search(text: string): Promise<void> {
    await this.table.search(this.searchLabel, text)
  }
}
