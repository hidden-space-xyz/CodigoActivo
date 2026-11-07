import { expect, type Locator, type Page, type Request } from '@playwright/test'

import { t } from '../../i18n'
import { AdminShell } from '../admin-shell'

/** A key figure of the dashboard, by the key of its tile. */
export type KpiKey =
  'users' | 'members' | 'inscriptions' | 'events' | 'activities' | 'resources' | 'news' | 'partners'

/** A preset of the dashboard time range. */
type RangePreset = '30d' | '90d' | '12m'

/** A card of the dashboard, by its test id. */
type DashboardCard = 'occupancy-card' | 'pending-signups-card'

/** The «Métricas» dashboard, at `/admin/dashboard`: key figures, range filter and cards. */
export class DashboardPage {
  /** The administration layout around the page. */
  readonly shell: AdminShell

  /** The group of time range buttons. */
  readonly range: Locator

  constructor(private readonly page: Page) {
    this.shell = new AdminShell(page)
    this.range = page.getByTestId('range-filter')
  }

  /** Opens the dashboard and waits for its figures. */
  async open(): Promise<void> {
    await this.shell.open('admin-dashboard')
    await expect(this.kpi('users')).toBeVisible()
  }

  /** The card of the key figure `key`. */
  kpi(key: KpiKey): Locator {
    return this.shell.main.getByTestId('kpi-card').filter({
      has: this.page
        .getByTestId('kpi-card-label')
        .filter({ hasText: t(`pages.admin.dashboard.kpi.${key}`) }),
    })
  }

  /** The line under a key figure that tells how many were added in the period. */
  kpiPeriod(key: KpiKey): Locator {
    return this.kpi(key).getByTestId('kpi-card-period')
  }

  /** The button of the range filter for the preset `preset`. */
  rangeButton(preset: RangePreset): Locator {
    return this.range
      .getByTestId('range-filter-preset')
      .filter({ hasText: t(`pages.admin.dashboard.range.preset${preset}`) })
  }

  /** The next request for the analytics of a time range. */
  analyticsRequest(): Promise<Request> {
    return this.page.waitForRequest(
      (request) => new URL(request.url()).pathname === '/api/reports/dashboard/analytics',
    )
  }

  /** The card `name`, such as the pending signups. */
  card(name: DashboardCard): Locator {
    return this.shell.main.getByTestId(name)
  }
}
