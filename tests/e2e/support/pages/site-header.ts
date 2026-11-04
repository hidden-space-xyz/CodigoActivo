import type { Locator, Page } from '@playwright/test'

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

  /** The link to the administration, for administrators. */
  readonly admin: Locator

  /** The sign-out button. */
  readonly logout: Locator

  /** The button that switches between the light and the dark theme. */
  readonly themeToggle: Locator

  /** The button that opens the menu drawer on narrow screens. */
  readonly menuButton: Locator

  constructor(page: Page) {
    this.root = page.getByRole('banner')
    this.navigation = this.root.getByRole('navigation', { name: 'Principal' })
    this.login = this.root.getByRole('link', { name: 'Iniciar sesión' })
    this.register = this.root.getByRole('link', { name: 'Regístrate' })
    this.account = this.root.getByRole('link', { name: 'Mi cuenta' })
    this.admin = this.root.getByRole('link', { name: 'Administración' })
    this.logout = this.root.getByRole('button', { name: 'Cerrar sesión' })
    this.themeToggle = this.root.getByRole('button', { name: /Cambiar a tema/ })
    this.menuButton = this.root.getByRole('button', { name: 'Abrir menú' })
  }

  /** The greeting «Hola, {name}». */
  greeting(firstName: string): Locator {
    return this.root.getByText(`Hola, ${firstName}`)
  }
}
