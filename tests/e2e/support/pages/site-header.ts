import type { Locator, Page } from '@playwright/test'

import { t } from '../i18n'

/** A section of the main navigation, by the name of its route. */
export type SiteSection = 'home' | 'news' | 'events' | 'resources' | 'about'

/** The public site header: main navigation, session links and the theme toggle. */
export class SiteHeader {
  /** The header landmark. */
  readonly root: Locator

  /** The main navigation. */
  readonly navigation: Locator

  /** The link to the login page, for visitors. */
  readonly login: Locator

  /** The link to the registration page, for visitors. */
  readonly register: Locator

  /** The link to «Mi cuenta», for signed-in people. */
  readonly account: Locator

  /** The link to the administration, for editors, administrators and the privacy officer. */
  readonly admin: Locator

  /** The sign-out button. */
  readonly logout: Locator

  /** The button that switches between the light and the dark theme. */
  readonly themeToggle: Locator

  /** The button that opens the menu drawer on narrow screens. */
  readonly menuButton: Locator

  constructor(page: Page) {
    this.root = page.getByTestId('site-header')
    this.navigation = this.root.getByTestId('site-nav')
    this.login = this.root.getByTestId('site-header-login')
    this.register = this.root.getByTestId('site-header-register')
    this.account = this.root.getByTestId('site-header-account')
    this.admin = this.root.getByTestId('site-header-admin')
    this.logout = this.root.getByTestId('site-header-logout')
    this.themeToggle = this.root.getByTestId('site-header-theme')
    this.menuButton = this.root.getByTestId('site-header-menu')
  }

  /** The navigation link to `section`. */
  link(section: SiteSection): Locator {
    return this.navigation.getByTestId(`nav-${section}`)
  }

  /** The greeting «Hola, {name}». */
  greeting(firstName: string): Locator {
    return this.root
      .getByTestId('site-header-greeting')
      .filter({ hasText: t('common.greeting', { name: firstName }) })
  }
}
