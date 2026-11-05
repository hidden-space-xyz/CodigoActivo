import type { Locator, Page } from '@playwright/test'

import type { Minor } from '../../data/people'
import { chooseOption } from '../../ui/element-plus'
import type { AccountPage } from './account-page'

/** The section «Menores a mi cargo» of the profile tab. */
export class MinorsSection {
  /** The section itself. */
  readonly root: Locator

  /** The button «+ Añadir menor». */
  readonly add: Locator

  /** The message shown while the account has no minors. */
  readonly empty: Locator

  constructor(account: AccountPage) {
    this.root = account.section('minors')
    this.add = this.root.getByTestId('minors-add')
    this.empty = this.root.getByTestId('minors-empty')
  }

  /** The list item of the minor whose full name is `name`. */
  minor(name: string): Locator {
    return this.root.getByTestId('minors-item').filter({ hasText: name })
  }
}

/** The dialog that adds a minor (`add`, «Añadir menor») or edits one (`edit`, «Editar menor»). */
export class MinorDialog {
  /** The dialog itself. */
  readonly root: Locator

  /** The first name field. */
  readonly firstName: Locator

  /** The last name field. */
  readonly lastName: Locator

  /** The native date input of the birth date. */
  readonly birthDate: Locator

  /** The birth date input while its value falls outside its `min`/`max` range. */
  readonly birthDateOutOfRange: Locator

  /** The gender select. */
  readonly gender: Locator

  /** The save button. */
  readonly save: Locator

  constructor(page: Page, mode: 'add' | 'edit') {
    this.root = page.getByTestId(`minor-dialog-${mode}`)
    this.firstName = this.root.getByTestId('minor-dialog-first-name')
    this.lastName = this.root.getByTestId('minor-dialog-last-name')
    this.birthDate = this.root.getByTestId('minor-dialog-birth-date')
    this.birthDateOutOfRange = this.birthDate.and(page.locator(':out-of-range'))
    this.gender = this.root.getByTestId('minor-dialog-gender')
    this.save = this.root.getByTestId('minor-dialog-save')
  }

  /** Fills in every field with `minor`. */
  async fill(minor: Minor): Promise<void> {
    await this.firstName.fill(minor.firstName)
    await this.lastName.fill(minor.lastName)
    await this.birthDate.fill(minor.birthDate)
    await chooseOption(this.gender, minor.gender)
  }
}
