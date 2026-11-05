import { signUp } from '../../support/api/signups'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { DashboardPage, type KpiKey } from '../../support/pages/admin-events/dashboard-page'
import { EventAdminPage } from '../../support/pages/admin-events/event-admin-page'

const KPIS: readonly KpiKey[] = [
  'users',
  'members',
  'inscriptions',
  'events',
  'activities',
  'resources',
  'news',
  'partners',
]

const IN_PERIOD = new RegExp(
  `^${t('pages.admin.dashboard.kpi.inRange', { n: '#' })
    .replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
    .replace('#', '[\\d.]+')}$`,
)

test.describe('admin dashboard', () => {
  test('the metrics show every key figure and switch between time ranges', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const dashboard = new DashboardPage(page)
    await dashboard.open()

    for (const key of KPIS) {
      await expect(dashboard.kpi(key)).toBeVisible()
    }
    await expect(dashboard.rangeButton('12m')).toHaveAttribute('aria-pressed', 'true')
    await expect(dashboard.kpiPeriod('users')).toHaveText(IN_PERIOD)

    await dashboard.rangeButton('30d').click()

    await expect(dashboard.rangeButton('30d')).toHaveAttribute('aria-pressed', 'true')
    await expect(dashboard.rangeButton('12m')).toHaveAttribute('aria-pressed', 'false')
    await expect(dashboard.kpiPeriod('users')).toHaveText(IN_PERIOD)

    await dashboard.rangeButton('90d').click()

    await expect(dashboard.rangeButton('90d')).toHaveAttribute('aria-pressed', 'true')
    await expect(dashboard.rangeButton('30d')).toHaveAttribute('aria-pressed', 'false')
  })

  test('an upcoming event with requested signups is pending and its occupancy listed', async ({
    pageAs,
    admin,
    seed,
    participant,
  }) => {
    const event = await seed.event()
    const activity = await seed.activity(event)
    await signUp(participant.api, activity, participant.account.id)
    const page = await pageAs(admin)
    const dashboard = new DashboardPage(page)
    await dashboard.open()

    const occupancy = dashboard.card('occupancy-card')
    await occupancy.getByTestId('occupancy-card-event').filter({ hasText: event.title }).click()
    await expect(
      occupancy.getByTestId('occupancy-card-activity').filter({ hasText: activity.title }),
    ).toBeVisible()
    const pending = dashboard
      .card('pending-signups-card')
      .getByTestId('pending-signups-card-event')
      .filter({ hasText: event.title })
    await expect(pending).toHaveAttribute('href', EventAdminPage.path(event.id))
    await expect(pending).toContainText(t('pages.admin.dashboard.pending.count', { n: 1 }))

    await pending.click()

    await expect(page).toHaveURL(EventAdminPage.path(event.id))
    await expect(new EventAdminPage(page).heading(event.title)).toBeVisible()
  })
})
