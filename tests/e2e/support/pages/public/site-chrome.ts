import type { Locator, Page } from '@playwright/test'

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
  /** The drawer, a dialog named «Menú». */
  readonly root: Locator

  /** The navigation inside the drawer. */
  readonly navigation: Locator

  constructor(page: Page) {
    this.root = page.getByRole('dialog', { name: 'Menú' })
    this.navigation = this.root.getByRole('navigation', { name: 'Principal' })
  }

  /** The link `name` of the drawer. */
  link(name: string): Locator {
    return this.root.getByRole('link', { name, exact: true })
  }
}
