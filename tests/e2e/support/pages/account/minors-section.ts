import type { Locator, Page } from '@playwright/test'

import type { Minor } from '../../data/people'
import { chooseOption } from '../../ui/element-plus'
import { GENDER_NAMES } from '../registration-page'
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
    this.root = account.section('Menores a mi cargo')
    this.add = this.root.getByRole('button', { name: '+ Añadir menor' })
    this.empty = this.root.getByText('No tienes menores a tu cargo.')
  }

  /** The list item of the minor whose full name is `name`. */
  minor(name: string): Locator {
    return this.root.locator('li.acc-minor').filter({ hasText: name })
  }
}

/** The dialog that adds a minor («Añadir menor») or edits one («Editar menor»). */
export class MinorDialog {
  /** The dialog itself. */
  readonly root: Locator

  /** The first name field. */
  readonly firstName: Locator

  /** The last name field. */
  readonly lastName: Locator

  /** The native date input `m-dob`. */
  readonly birthDate: Locator

  /** The birth date input while its value falls outside its `min`/`max` range. */
  readonly birthDateOutOfRange: Locator

  /** The gender select. */
  readonly gender: Locator

  /** The save button. */
  readonly save: Locator

  constructor(page: Page, title: 'Añadir menor' | 'Editar menor') {
    this.root = page.getByRole('dialog', { name: title })
    this.firstName = this.root.getByLabel('Nombre', { exact: true })
    this.lastName = this.root.getByLabel('Apellidos', { exact: true })
    this.birthDate = this.root.getByLabel('Fecha de nacimiento')
    this.birthDateOutOfRange = this.root.locator('input#m-dob:out-of-range')
    this.gender = this.root.getByLabel('Género')
    this.save = this.root.getByRole('button', { name: 'Guardar', exact: true })
  }

  /** Fills in every field with `minor`. */
  async fill(minor: Minor): Promise<void> {
    await this.firstName.fill(minor.firstName)
    await this.lastName.fill(minor.lastName)
    await this.birthDate.fill(minor.birthDate)
    await chooseOption(this.gender, GENDER_NAMES[minor.gender])
  }
}
