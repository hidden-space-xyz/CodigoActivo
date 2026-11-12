import { expect, type Locator, type Page, type Request, type Response } from '@playwright/test'

/** An entry of the administration sidebar, by the name of its route. */
export type AdminSection =
  | 'admin-dashboard'
  | 'admin-events'
  | 'admin-news'
  | 'admin-partners'
  | 'admin-resources'
  | 'admin-users'
  | 'admin-inventory'
  | 'admin-diagnostics'
  | 'admin-audit'
  | 'admin-blocked-data'
  | 'admin-legal-policies'
  | 'admin-catalogs'

const PATHS: Readonly<Record<AdminSection, string>> = {
  'admin-dashboard': '/admin/dashboard',
  'admin-events': '/admin/events',
  'admin-news': '/admin/news',
  'admin-partners': '/admin/partners',
  'admin-resources': '/admin/resources',
  'admin-users': '/admin/users',
  'admin-inventory': '/admin/inventory',
  'admin-diagnostics': '/admin/diagnostics',
  'admin-audit': '/admin/audit',
  'admin-blocked-data': '/admin/blocked-data',
  'admin-legal-policies': '/admin/legal',
  'admin-catalogs': '/admin/catalogs',
}

/** The administration layout: the sidebar, the top bar and the page header. */
export class AdminShell {
  /** The sidebar navigation. */
  readonly sidebar: Locator

  /** The sidebar links, in display order. */
  readonly links: Locator

  /** The main landmark of the current admin page. */
  readonly main: Locator

  /** The sign-out button of the top bar. */
  readonly logout: Locator

  constructor(private readonly page: Page) {
    this.sidebar = page.getByTestId('admin-nav')
    this.links = this.sidebar.locator('[data-testid^="nav-"]')
    this.main = page.locator('main')
    this.logout = page.getByTestId('admin-shell-logout')
  }

  /** Opens the admin page of `section` directly and waits for it. */
  async open(section: AdminSection): Promise<void> {
    await this.page.goto(PATHS[section])
    await expect(this.view(section)).toBeVisible()
  }

  /** The content of the admin page of `section`, shown only while it is the current page. */
  view(section: AdminSection): Locator {
    return this.page.getByTestId(`page-${section}`)
  }

  /** The sidebar link to `section`. */
  link(section: AdminSection): Locator {
    return this.sidebar.getByTestId(`nav-${section}`)
  }

  /** The sections of the sidebar, in display order. */
  sections(): Promise<string[]> {
    return this.links.evaluateAll((links) =>
      links.map((link) => (link.getAttribute('data-testid') ?? '').replace(/^nav-/, '')),
    )
  }

  /**
   * Follows the sidebar link of each of `sections` and expects its page to open and the API to
   * answer every request it makes, which proves the SPA and the API agree on who may use it.
   */
  async expectSectionsToWork(sections: readonly AdminSection[]): Promise<void> {
    const pending = new Set<Request>()
    const refused: string[] = []
    const isApi = (request: Request) => new URL(request.url()).pathname.startsWith('/api/')
    this.page.on('request', (request) => {
      if (isApi(request)) pending.add(request)
    })
    this.page.on('response', (response: Response) => {
      if (isApi(response.request()) && !response.ok()) {
        refused.push(`${response.status()} ${response.url()}`)
      }
    })
    this.page.on('requestfinished', (request) => pending.delete(request))
    this.page.on('requestfailed', (request) => pending.delete(request))
    for (const section of sections) {
      await this.link(section).click()
      await expect(this.page).toHaveURL(PATHS[section])
      await expect(this.view(section)).toBeVisible()
      await expect.poll(() => pending.size).toBe(0)
    }
    expect(refused).toStrictEqual([])
  }

  /** The path of the admin page of `section`. */
  static path(section: AdminSection): string {
    return PATHS[section]
  }
}
