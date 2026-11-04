import { signUp } from '../../support/api/signups'
import { expect, test } from '../../support/fixtures/test'
import { DashboardPage, type KpiLabel } from '../../support/pages/admin-events/dashboard-page'
import { EventAdminPage } from '../../support/pages/admin-events/event-admin-page'

const KPIS: readonly KpiLabel[] = [
  'Usuarios',
  'Socios',
  'Inscripciones',
  'Eventos',
  'Actividades',
  'Recursos',
  'Novedades',
  'Patrocinadores',
]

test.describe('admin dashboard', () => {
  test('the metrics show every key figure and switch between time ranges', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const dashboard = new DashboardPage(page)
    await dashboard.open()

    for (const label of KPIS) {
      await expect(dashboard.kpi(label)).toBeVisible()
    }
    await expect(dashboard.rangeButton('12 meses')).toHaveAttribute('aria-pressed', 'true')
    await expect(dashboard.kpiPeriod('Usuarios')).toHaveText(/^\+[\d.]+ en el periodo$/)

    await dashboard.rangeButton('30 días').click()

    await expect(dashboard.rangeButton('30 días')).toHaveAttribute('aria-pressed', 'true')
    await expect(dashboard.rangeButton('12 meses')).toHaveAttribute('aria-pressed', 'false')
    await expect(dashboard.kpiPeriod('Usuarios')).toHaveText(/^\+[\d.]+ en el periodo$/)

    await dashboard.rangeButton('90 días').click()

    await expect(dashboard.rangeButton('90 días')).toHaveAttribute('aria-pressed', 'true')
    await expect(dashboard.rangeButton('30 días')).toHaveAttribute('aria-pressed', 'false')
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

    const occupancy = dashboard.card('Ocupación de próximas actividades')
    await occupancy.getByRole('button', { name: event.title }).click()
    await expect(occupancy.getByText(activity.title, { exact: true })).toBeVisible()
    const pending = dashboard
      .card('Solicitudes pendientes')
      .getByRole('link', { name: event.title })
    await expect(pending).toHaveAttribute('href', EventAdminPage.path(event.id))
    await expect(pending).toContainText('1 pendiente')

    await pending.click()

    await expect(page).toHaveURL(EventAdminPage.path(event.id))
    await expect(new EventAdminPage(page).heading(event.title)).toBeVisible()
  })
})
