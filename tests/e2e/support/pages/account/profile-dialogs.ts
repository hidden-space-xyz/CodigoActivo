import type { Locator, Page } from '@playwright/test'
import type { Gender } from '@contract/gender'

import { nationalId } from '../../data/people'

const NATIONAL_ID_LETTERS = 'TRWAGMYFPDXBNJZSQVHLCKE'

/** The dialog «Editar mis datos» of the profile. */
export class ProfileDialog {
  /** The dialog itself. */
  readonly root: Locator

  /** The first name field. */
  readonly firstName: Locator

  /** The last name field. */
  readonly lastName: Locator

  /** The DNI/NIE field. */
  readonly nationalId: Locator

  /** The gender select. */
  readonly gender: Locator

  /** The phone field. */
  readonly phone: Locator

  /** The email field. */
  readonly email: Locator

  /** The checkbox that accepts the news by email. */
  readonly newsConsent: Locator

  /** «Tu contraseña actual», asked only when the email or a phone changes. */
  readonly currentPassword: Locator

  /** The save button. */
  readonly save: Locator

  /** The cancel button. */
  readonly cancel: Locator

  constructor(page: Page) {
    this.root = page.getByRole('dialog', { name: 'Editar mis datos' })
    this.firstName = this.root.getByLabel('Nombre', { exact: true })
    this.lastName = this.root.getByLabel('Apellidos', { exact: true })
    this.nationalId = this.root.getByLabel('DNI/NIE')
    this.gender = this.root.getByLabel('Género')
    this.phone = this.root.getByLabel('Teléfono', { exact: true })
    this.email = this.root.getByLabel('Correo', { exact: true })
    this.newsConsent = this.root.getByRole('checkbox', {
      name: /Quiero recibir por email novedades/,
    })
    this.currentPassword = this.root.getByLabel('Tu contraseña actual')
    this.save = this.root.getByRole('button', { name: 'Guardar', exact: true })
    this.cancel = this.root.getByRole('button', { name: 'Cancelar', exact: true })
  }
}

/** The dialog «Cambiar contraseña» of the profile. */
export class PasswordDialog {
  /** The dialog itself. */
  readonly root: Locator

  /** «Contraseña actual». */
  readonly current: Locator

  /** «Nueva contraseña». */
  readonly next: Locator

  /** «Repite la nueva contraseña». */
  readonly confirmation: Locator

  /** The error the dialog shows for a broken rule or a refused change. */
  readonly error: Locator

  /** The save button. */
  readonly save: Locator

  constructor(page: Page) {
    this.root = page.getByRole('dialog', { name: 'Cambiar contraseña' })
    this.current = this.root.getByLabel('Contraseña actual')
    this.next = this.root.getByLabel('Nueva contraseña', { exact: true })
    this.confirmation = this.root.getByLabel('Repite la nueva contraseña')
    this.error = this.root.getByRole('alert')
    this.save = this.root.getByRole('button', { name: 'Guardar', exact: true })
  }

  /** Fills in the three fields and sends the form. */
  async submit(current: string, next: string, confirmation: string = next): Promise<void> {
    await this.current.fill(current)
    await this.next.fill(next)
    await this.confirmation.fill(confirmation)
    await this.save.click()
  }
}

/** A gender other than `gender`, to change it in a form. */
export function otherGender(gender: Gender): Gender {
  return gender === 'Female' ? 'Male' : 'Female'
}

/** A DNI shaped correctly whose control letter does not match its number. */
export function mismatchedNationalId(): string {
  const valid = nationalId()
  const letter = valid.charAt(8)
  const wrong = NATIONAL_ID_LETTERS.charAt((NATIONAL_ID_LETTERS.indexOf(letter) + 1) % 23)
  return `${valid.slice(0, 8)}${wrong}`
}
