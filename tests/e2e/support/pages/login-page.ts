import { expect, type Locator, type Page } from '@playwright/test'

import { loginCodeQuery, type Credentials } from '../api/accounts'
import { codeIn, type Mailbox } from '../mail/mailbox'

const SESSION_COOKIE = 'CodigoActivo.Session'

/** The password step of the login, at `/login`. */
export class LoginPage {
  /** The content of the login page, shown only while it is the current page. */
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
    this.heading = page.getByTestId('page-login')
    this.email = page.getByTestId('login-email')
    this.password = page.getByTestId('login-password')
    this.keepSignedIn = page.getByTestId('login-keep-signed-in')
    this.submit = page.getByTestId('login-submit')
    this.error = page.getByTestId('login-error')
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
  /** The content of the second step, shown only once the password was accepted. */
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
    this.heading = page.getByTestId('page-login-two-factor')
    this.code = page.getByTestId('login-two-factor-code')
    this.submit = page.getByTestId('login-two-factor-submit')
    this.resend = page.getByTestId('login-two-factor-resend')
    this.error = page.getByTestId('login-two-factor-error')
  }

  /** Types `code` and sends it. */
  async enter(code: string): Promise<void> {
    await this.code.fill(code)
    await this.submit.click()
  }
}

/** When the session cookie of `page` expires, in seconds, or -1 when it ends with the browser. */
export async function sessionExpiry(page: Page): Promise<number | undefined> {
  const cookies = await page.context().cookies()
  return cookies.find((cookie) => cookie.name === SESSION_COOKIE)?.expires
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
  const email = await mailbox.waitFor(loginCodeQuery(credentials.email, sentAfter))
  await twoFactor.enter(codeIn(email))
  await expect(page).not.toHaveURL(/\/login/)
}
