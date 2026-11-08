import type { Locator, Page } from '@playwright/test'

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

  /** The error the DNI/NIE field shows for a value it refuses. */
  readonly nationalIdError: Locator

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

  /** The error of «Tu contraseña actual», for a missing or refused password. */
  readonly currentPasswordError: Locator

  /** «Código de verificación», asked only when the email changes. */
  readonly code: Locator

  /** «Enviar código», which emails the code to the current address. */
  readonly sendCode: Locator

  /** The error of «Código de verificación», for a missing, refused or unsent code. */
  readonly codeError: Locator

  /** The save button. */
  readonly save: Locator

  /** The cancel button. */
  readonly cancel: Locator

  constructor(page: Page) {
    this.root = page.getByTestId('profile-dialog')
    this.firstName = this.root.getByTestId('profile-dialog-first-name')
    this.lastName = this.root.getByTestId('profile-dialog-last-name')
    const nationalIdField = this.root.getByTestId('profile-dialog-national-id')
    this.nationalId = nationalIdField.locator('input')
    this.nationalIdError = nationalIdField.getByTestId('national-id-input-error')
    this.gender = this.root.getByTestId('profile-dialog-gender')
    this.phone = this.root.getByTestId('profile-dialog-phone')
    this.email = this.root.getByTestId('profile-dialog-email')
    this.newsConsent = this.root.getByTestId('profile-dialog-news-consent')
    this.currentPassword = this.root.getByTestId('profile-dialog-current-password')
    this.currentPasswordError = this.root.getByTestId('profile-dialog-current-password-error')
    this.code = this.root.getByTestId('profile-dialog-code')
    this.sendCode = this.root.getByTestId('profile-dialog-send-code')
    this.codeError = this.root.getByTestId('profile-dialog-code-error')
    this.save = this.root.getByTestId('profile-dialog-save')
    this.cancel = this.root.getByTestId('profile-dialog-cancel')
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
    this.root = page.getByTestId('password-dialog')
    this.current = this.root.getByTestId('password-dialog-current')
    this.next = this.root.getByTestId('password-dialog-new')
    this.confirmation = this.root.getByTestId('password-dialog-confirm')
    this.error = this.root.getByTestId('password-dialog-error')
    this.save = this.root.getByTestId('password-dialog-save')
  }

  /** Fills in the three fields and sends the form. */
  async submit(current: string, next: string, confirmation: string = next): Promise<void> {
    await this.current.fill(current)
    await this.next.fill(next)
    await this.confirmation.fill(confirmation)
    await this.save.click()
  }
}
