import { expect, type Locator, type Page } from '@playwright/test'

import type { Credentials } from '../api/accounts'
import { codeIn, SUBJECTS, type Mailbox } from '../mail/mailbox'

/** The password step of the login, at `/login`. */
export class LoginPage {
  /** «Bienvenido de nuevo», shown once the page has loaded. */
  readonly heading: Locator

  /** The email field. */
  readonly email: Locator

  /** The password field. */
  readonly password: Locator

  /** The «keep me signed in» checkbox. */
  readonly keepSignedIn: Locator

  /** The submit button. */
  readonly submit: Locator

  /** The error the form shows after a refused attempt. */
  readonly error: Locator

  constructor(private readonly page: Page) {
    this.heading = page.getByRole('heading', { name: 'Bienvenido de nuevo' })
    this.email = page.getByLabel('Correo electrónico')
    this.password = page.getByLabel('Contraseña', { exact: true })
    this.keepSignedIn = page.getByRole('checkbox', { name: /Mantener la sesión iniciada/ })
    this.submit = page.locator('form').getByRole('button', { name: 'Iniciar sesión' })
    this.error = page.locator('form').getByRole('alert')
  }

  /** Opens the login page, optionally with the `redirect` it returns to. */
  async goto(redirect?: string): Promise<void> {
    await this.page.goto(redirect ? `/login?redirect=${encodeURIComponent(redirect)}` : '/login')
    await expect(this.heading).toBeVisible()
  }

  /** Fills in and sends the password step. */
  async submitPassword(credentials: Credentials): Promise<void> {
    await this.email.fill(credentials.email)
    await this.password.fill(credentials.password)
    await this.submit.click()
  }
}

/** The second step of the login, at `/login/verify`. */
export class TwoFactorPage {
  /** «Un paso más», shown once the password was accepted. */
  readonly heading: Locator

  /** The code field. */
  readonly code: Locator

  /** The submit button. */
  readonly submit: Locator

  /** The button that asks for another emailed code. */
  readonly resend: Locator

  /** The error the form shows after a refused code. */
  readonly error: Locator

  constructor(page: Page) {
    this.heading = page.getByRole('heading', { name: 'Un paso más' })
    this.code = page.getByLabel('Código de verificación')
    this.submit = page.getByRole('button', { name: 'Verificar' })
    this.resend = page.getByRole('button', { name: /pide otro código/ })
    this.error = page.locator('form').getByRole('alert')
  }

  /** Types `code` and sends it. */
  async enter(code: string): Promise<void> {
    await this.code.fill(code)
    await this.submit.click()
  }
}

/**
 * Signs in through both login pages as a person would: the password, then the code emailed to
 * them. Ends once the browser has left the login pages.
 */
export async function signInThroughPages(
  page: Page,
  mailbox: Mailbox,
  credentials: Credentials,
): Promise<void> {
  const sentAfter = new Date()
  await new LoginPage(page).submitPassword(credentials)
  const twoFactor = new TwoFactorPage(page)
  await expect(twoFactor.heading).toBeVisible()
  const email = await mailbox.waitFor({
    to: credentials.email,
    subject: SUBJECTS.loginCode,
    sentAfter,
    reusedAfter: new Date(sentAfter.getTime() - 60_000),
  })
  await twoFactor.enter(codeIn(email))
  await expect(page).not.toHaveURL(/\/login/)
}
