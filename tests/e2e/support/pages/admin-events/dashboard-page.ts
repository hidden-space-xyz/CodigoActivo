import { expect, type Locator, type Page } from '@playwright/test'

import { AdminShell } from '../admin-shell'

/** A key figure of the dashboard. */
export type KpiLabel =
  | 'Usuarios'
  | 'Socios'
  | 'Inscripciones'
  | 'Eventos'
  | 'Actividades'
  | 'Recursos'
  | 'Novedades'
  | 'Patrocinadores'

/** A preset of the dashboard time range. */
type RangeLabel = '30 días' | '90 días' | '12 meses'

/** The «Métricas» dashboard, at `/admin/dashboard`: key figures, range filter and cards. */
export class DashboardPage {
  /** The administration layout around the page. */
  readonly shell: AdminShell

  /** The group of time range buttons. */
  readonly range: Locator

  constructor(private readonly page: Page) {
    this.shell = new AdminShell(page)
    this.range = page.getByRole('group', { name: 'Rango de tiempo' })
  }

  /** Opens the dashboard and waits for its figures. */
  async open(): Promise<void> {
    await this.shell.open('Métricas')
    await expect(this.kpi('Usuarios')).toBeVisible()
  }

  /** The card of the key figure `label`. */
  kpi(label: KpiLabel): Locator {
    return this.shell.main
      .locator('article.kpi-card')
      .filter({ has: this.page.locator('.kpi-card__label').getByText(label, { exact: true }) })
  }

  /** The line under a key figure that tells how many were added in the period. */
  kpiPeriod(label: KpiLabel): Locator {
    return this.kpi(label).locator('.kpi-card__foot')
  }

  /** The preset button `label` of the range filter. */
  rangeButton(label: RangeLabel): Locator {
    return this.range.getByRole('button', { name: label, exact: true })
  }

  /** The card under the heading `title`, such as «Solicitudes pendientes». */
  card(title: string): Locator {
    return this.shell.main
      .locator('section.chart-card')
      .filter({ has: this.page.getByRole('heading', { name: title, exact: true }) })
  }
}
