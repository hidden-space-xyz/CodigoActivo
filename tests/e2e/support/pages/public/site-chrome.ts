import type { Locator, Page } from '@playwright/test'

import type { SiteSection } from '../site-header'

/** The colour theme of the site: the `ca-dark` class on the root element and the saved choice. */
export class SiteTheme {
  /** The `<html>` element, which carries `ca-dark` while the dark theme is on. */
  readonly root: Locator

  constructor(private readonly page: Page) {
    this.root = page.locator('html')
  }

  /** The theme saved in `localStorage` under `ca-theme`, or `null` when none was chosen. */
  saved(): Promise<string | null> {
    return this.page.evaluate(() => localStorage.getItem('ca-theme'))
  }
}

/** The menu drawer that holds the navigation on narrow screens. */
export class MenuDrawer {
  /** The panel that slides in with the navigation, present only while it is open. */
  readonly root: Locator

  /** The greeting of signed-in people. */
  readonly greeting: Locator

  /** The link to the administration, for editors, administrators and the privacy officer. */
  readonly admin: Locator

  /** The link to «Mi cuenta», for signed-in people. */
  readonly account: Locator

  /** The sign-out button. */
  readonly logout: Locator

  /** The link to the registration page, for visitors. */
  readonly register: Locator

  /** The link to the login page, for visitors. */
  readonly login: Locator

  constructor(page: Page) {
    this.root = page.getByTestId('site-menu')
    this.greeting = this.root.getByTestId('site-menu-greeting')
    this.admin = this.root.getByTestId('site-menu-admin')
    this.account = this.root.getByTestId('site-menu-account')
    this.logout = this.root.getByTestId('site-menu-logout')
    this.register = this.root.getByTestId('site-menu-register')
    this.login = this.root.getByTestId('site-menu-login')
  }

  /** The navigation link to `section`. */
  link(section: SiteSection): Locator {
    return this.root.getByTestId(`nav-${section}`)
  }
}
