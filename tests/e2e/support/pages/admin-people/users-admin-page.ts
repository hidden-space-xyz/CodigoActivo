import { expect, type Locator, type Page } from '@playwright/test'

import { escapeRegExp } from '../../data/formats'
import { fullName, type Named } from '../../data/people'
import { DataTable } from '../../ui/data-table'
import { AdminShell } from '../admin-shell'

/** Which people the user list shows, by the value of its option in the scope switch. */
export type UserScope = 'independent' | 'dependent' | 'all'

/**
 * The admin user list, at `/admin/users`: the scope switch, the toolbar and column filters, the
 * rows with their badges and family links, and the row menu of each person.
 */
export class UsersAdminPage {
  /** The table with its toolbar, chips and rows. */
  readonly table: DataTable

  /** The switch «Mostrar usuarios». */
  readonly scopes: Locator

  /** The toolbar button that emails every filtered user. */
  readonly bulkEmail: Locator

  constructor(private readonly page: Page) {
    this.table = new DataTable(page)
    this.scopes = page.getByTestId('users-scope')
    this.bulkEmail = this.table.root.getByTestId('users-bulk-email')
  }

  /** Opens the page and waits for the first list. */
  async open(): Promise<void> {
    await new AdminShell(this.page).open('admin-users')
    await this.table.expectSettled()
  }

  /** Searches the list by name. */
  async find(name: string): Promise<void> {
    await this.table.search(name)
  }

  /** The radio of `scope` in the switch, checked while the list shows that scope. */
  scope(scope: UserScope): Locator {
    return this.scopes
      .locator('label')
      .filter({ has: this.page.getByTestId(`users-scope-${scope}`) })
      .locator('input')
  }

  /** Switches the list to `scope` by clicking its label, as a person does. */
  async showScope(scope: UserScope): Promise<void> {
    await this.scopes.getByTestId(`users-scope-${scope}`).click()
    await expect(this.scope(scope)).toBeChecked()
    await this.table.expectSettled()
  }

  /**
   * The row of `person`, the one that starts with their name: a dependent's row also names their
   * guardian after «Tutor:».
   */
  row(person: Named): Locator {
    return this.table.row(new RegExp(`^\\s*${escapeRegExp(fullName(person))}`))
  }

  /** The button that opens the row menu of `person`. */
  menuButton(person: Named): Locator {
    return this.row(person).getByTestId('row-actions-menu')
  }

  /** Opens the row menu of `person`. */
  async openMenu(person: Named): Promise<void> {
    await this.menuButton(person).click()
    await expect(this.page.locator('.row-actions__menu').filter({ visible: true })).toBeVisible()
  }

  /** The entry of the open row menu that runs the row action `key`, such as `changeType`. */
  menuItem(key: string): Locator {
    return this.table.menuItem(key)
  }

  /** Runs the row menu entry of the row action `key` on the row of `person`. */
  async act(person: Named, key: string): Promise<void> {
    await this.openMenu(person)
    await this.menuItem(key).click()
  }
}
