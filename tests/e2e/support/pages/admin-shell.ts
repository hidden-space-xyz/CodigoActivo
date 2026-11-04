import { expect, type Locator, type Page } from '@playwright/test'

/** An entry of the administration sidebar. */
export type AdminSection =
  | 'Métricas'
  | 'Eventos'
  | 'Novedades'
  | 'Patrocinadores'
  | 'Recursos'
  | 'Usuarios'
  | 'Diagnóstico'
  | 'Auditoría'
  | 'Archivo legal'
  | 'Políticas'
  | 'Catálogos'

const PATHS: Readonly<Record<AdminSection, string>> = {
  Métricas: '/admin/dashboard',
  Eventos: '/admin/events',
  Novedades: '/admin/news',
  Patrocinadores: '/admin/partners',
  Recursos: '/admin/resources',
  Usuarios: '/admin/users',
  Diagnóstico: '/admin/diagnostics',
  Auditoría: '/admin/audit',
  'Archivo legal': '/admin/blocked-data',
  Políticas: '/admin/legal',
  Catálogos: '/admin/catalogs',
}

/** The administration layout: the sidebar, the top bar and the page header. */
export class AdminShell {
  /** The sidebar navigation. */
  readonly sidebar: Locator

  /** The main landmark of the current admin page. */
  readonly main: Locator

  /** The sign-out button of the top bar. */
  readonly logout: Locator

  constructor(private readonly page: Page) {
    this.sidebar = page.getByRole('complementary').getByRole('navigation')
    this.main = page.getByRole('main')
    this.logout = page.getByRole('button', { name: 'Cerrar sesión' })
  }

  /** Opens the admin page of `section` directly and waits for its heading. */
  async open(section: AdminSection, heading: string = section): Promise<void> {
    await this.page.goto(PATHS[section])
    await expect(this.heading(heading)).toBeVisible()
  }

  /** The `h1` of the current admin page. */
  heading(name: string): Locator {
    return this.main.getByRole('heading', { level: 1, name, exact: true })
  }

  /** The sidebar link to `section`. */
  link(section: AdminSection): Locator {
    return this.sidebar.getByRole('link', { name: section, exact: true })
  }

  /** The path of the admin page of `section`. */
  static path(section: AdminSection): string {
    return PATHS[section]
  }
}
