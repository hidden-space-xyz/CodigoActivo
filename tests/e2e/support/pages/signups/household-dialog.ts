import type { Locator, Page } from '@playwright/test'

import { CATALOG, type RoleName } from '../../api/catalogs'
import { escapeRegExp } from '../../data/formats'
import { chooseOption } from '../../ui/element-plus'
import { expectSelectOptions } from './activity-card'

/**
 * The dialog «¿A quién quieres apuntar?» a guardian signs the household up with: a row per person,
 * with a checkbox named after them and a role select, or a note when they are already signed up.
 */
export class HouseholdDialog {
  /** The dialog box. */
  readonly root: Locator

  /** «Apuntar», which sends the signups of the ticked people. */
  readonly submit: Locator

  constructor(private readonly page: Page) {
    this.root = page.getByTestId('household-dialog')
    this.submit = this.root.getByTestId('household-dialog-submit')
  }

  /** The checkbox of the person shown as `name`. */
  person(name: string): Locator {
    return this.row(name).getByTestId('household-dialog-person')
  }

  /** The row of the person shown as `name`. */
  row(name: string): Locator {
    const shownAs = new RegExp(`^\\s*${escapeRegExp(name)}\\s*$`)
    return this.root.getByTestId('household-dialog-row').filter({
      has: this.page.getByTestId('household-dialog-member').filter({ hasText: shownAs }),
    })
  }

  /** Picks `role` in the role select of the person shown as `name`. */
  async chooseRole(name: string, role: RoleName): Promise<void> {
    await chooseOption(this.row(name).getByTestId('household-dialog-role'), CATALOG.roles[role])
  }

  /** Checks the role select of the person shown as `name` offers exactly `roles`. */
  async expectRoles(name: string, roles: readonly RoleName[]): Promise<void> {
    await expectSelectOptions(
      this.page,
      this.row(name).getByTestId('household-dialog-role'),
      roles.map((role) => CATALOG.roles[role]),
    )
  }
}
