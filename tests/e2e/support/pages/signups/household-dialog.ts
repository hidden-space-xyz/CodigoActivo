import type { Locator, Page } from '@playwright/test'

import { chooseOption, dialog } from '../../ui/element-plus'
import { expectSelectOptions } from './activity-card'

/**
 * The dialog «¿A quién quieres apuntar?» a guardian signs the household up with: a row per person,
 * with a checkbox named after them and a role select, or a note when they are already signed up.
 */
export class HouseholdDialog {
  /** The dialog box, found by its title. */
  readonly root: Locator

  /** «Apuntar», which sends the signups of the ticked people. */
  readonly submit: Locator

  constructor(private readonly page: Page) {
    this.root = dialog(page, '¿A quién quieres apuntar?')
    this.submit = this.root.getByRole('button', { name: 'Apuntar', exact: true })
  }

  /** The checkbox of the person shown as `name`. */
  person(name: string): Locator {
    return this.root.getByRole('checkbox', { name, exact: true })
  }

  /** The row of the person shown as `name`. */
  row(name: string): Locator {
    return this.root
      .getByRole('listitem')
      .filter({ has: this.page.getByText(name, { exact: true }) })
  }

  /** Picks `role` in the role select of the person shown as `name`. */
  async chooseRole(name: string, role: string): Promise<void> {
    await chooseOption(this.row(name).getByRole('combobox'), role)
  }

  /** Checks the role select of the person shown as `name` offers exactly `roles`. */
  async expectRoles(name: string, roles: readonly string[]): Promise<void> {
    await expectSelectOptions(this.page, this.row(name).getByRole('combobox'), roles)
  }
}
