import { expect, type Locator, type Page } from '@playwright/test'

import { fullName, type Named } from '../../data/people'
import { DataTable } from '../../ui/data-table'
import { AdminShell } from '../admin-shell'

/** Which people the user list shows. */
export type UserScope = 'Independientes' | 'Dependientes' | 'Todos'

/**
 * The admin user list, at `/admin/users`: the scope switch, the toolbar and column filters, the
 * rows with their badges and family links, and the row menu «Más acciones para {name}».
 */
export class UsersAdminPage {
  /** The table with its toolbar, chips and rows. */
  readonly table: DataTable

  /** The radio group «Mostrar usuarios». */
  readonly scopes: Locator

  /** The toolbar button that emails every filtered user. */
  readonly bulkEmail: Locator

  constructor(private readonly page: Page) {
    this.table = new DataTable(page)
    this.scopes = page.getByRole('radiogroup', { name: 'Mostrar usuarios' })
    this.bulkEmail = this.table.root.getByRole('button', { name: 'Enviar correo', exact: true })
  }

  /** Opens the page and waits for the first list. */
  async open(): Promise<void> {
    await new AdminShell(this.page).open('Usuarios')
    await this.table.expectSettled()
  }

  /** Searches the list by name. */
  async find(name: string): Promise<void> {
    await this.table.search('Buscar por nombre', name)
  }

  /** Switches the list to `scope` by clicking its label, as a person does. */
  async showScope(scope: UserScope): Promise<void> {
    await this.scopes.getByText(scope, { exact: true }).click()
    await expect(this.scopes.getByRole('radio', { name: scope, exact: true })).toBeChecked()
    await this.table.expectSettled()
  }

  /**
   * The row of `person`, the one that starts with their name: a dependent's row also names their
   * guardian after «Tutor:».
   */
  row(person: Named): Locator {
    return this.table.row(new RegExp(`^\\s*${escaped(fullName(person))}`))
  }

  /** The button that opens the row menu of `person`. */
  menuButton(person: Named): Locator {
    return this.table.root.getByRole('button', { name: `Más acciones para ${fullName(person)}` })
  }

  /** Opens the row menu of `person`. */
  async openMenu(person: Named): Promise<void> {
    await this.menuButton(person).click()
    await expect(this.table.menuItem(/.+/).first()).toBeVisible()
  }

  /** The entry of the open row menu whose name starts with `action`. */
  menuItem(action: string): Locator {
    return this.table.menuItem(new RegExp(`^${escaped(action)}`))
  }

  /** Runs the row menu entry `action` on the row of `person`. */
  async act(person: Named, action: string): Promise<void> {
    await this.openMenu(person)
    await this.menuItem(action).click()
  }
}

function escaped(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}
