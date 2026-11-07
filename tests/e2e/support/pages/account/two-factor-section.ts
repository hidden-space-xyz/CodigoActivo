import { expect, type Locator, type Page } from '@playwright/test'

import { AuthenticatorApp } from '../../api/my-account'
import type { AccountPage } from './account-page'

/** The section «Verificación en dos pasos» of the profile tab. */
export class TwoFactorSection {
  /** The section itself. */
  readonly root: Locator

  /** The current method, such as «Código por correo electrónico». */
  readonly method: Locator

  /** The button that starts enrolling an authenticator application. */
  readonly useAuthenticator: Locator

  /** The button that goes back to emailed codes. */
  readonly useEmail: Locator

  constructor(account: AccountPage) {
    this.root = account.section('two-factor')
    this.method = this.root.getByTestId('two-factor-method')
    this.useAuthenticator = this.root.getByTestId('two-factor-use-authenticator')
    this.useEmail = this.root.getByTestId('two-factor-use-email')
  }
}

/**
 * The dialog «Configurar aplicación de autenticación»: the password step unlocks the shared key and
 * the code step activates the application with its first code.
 */
export class AuthenticatorSetupDialog {
  /** The dialog itself. */
  readonly root: Locator

  /** «Tu contraseña», in the password step. */
  readonly password: Locator

  /** «Continuar», which sends the password step. */
  readonly continue: Locator

  /** The Base32 key to type into the application, in the code step. */
  readonly sharedKey: Locator

  /** «Código de la aplicación», in the code step. */
  readonly code: Locator

  /** «Activar», which sends the code step. */
  readonly activate: Locator

  /** The error the dialog shows for a refused request. */
  readonly error: Locator

  constructor(page: Page) {
    this.root = page.getByTestId('two-factor-setup-dialog')
    this.password = this.root.getByTestId('two-factor-setup-password')
    this.continue = this.root.getByTestId('two-factor-setup-continue')
    this.sharedKey = this.root.getByTestId('two-factor-shared-key')
    this.code = this.root.getByTestId('two-factor-setup-code')
    this.activate = this.root.getByTestId('two-factor-setup-activate')
    this.error = this.root.getByTestId('two-factor-setup-error')
  }

  /** Sends the password step and enrolls an application with the key the dialog then shows. */
  async unlock(password: string): Promise<AuthenticatorApp> {
    await this.password.fill(password)
    await this.continue.click()
    await expect(this.sharedKey).toHaveText(/^[A-Z2-7 ]{16,}$/)
    return new AuthenticatorApp(await this.sharedKey.innerText())
  }
}

/** The dialog «Volver al código por correo», which needs the password and a current code. */
export class DisableAuthenticatorDialog {
  /** The dialog itself. */
  readonly root: Locator

  /** «Tu contraseña». */
  readonly password: Locator

  /** «Código de la aplicación». */
  readonly code: Locator

  /** The submit button, named like the dialog. */
  readonly submit: Locator

  /** The error the dialog shows for a refused request. */
  readonly error: Locator

  constructor(page: Page) {
    this.root = page.getByTestId('two-factor-disable-dialog')
    this.password = this.root.getByTestId('two-factor-disable-password')
    this.code = this.root.getByTestId('two-factor-disable-code')
    this.submit = this.root.getByTestId('two-factor-disable-submit')
    this.error = this.root.getByTestId('two-factor-disable-error')
  }
}
