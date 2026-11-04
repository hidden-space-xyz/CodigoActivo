import { expect, test } from '../../support/fixtures/test'
import { AdminShell, type AdminSection } from '../../support/pages/admin-shell'

const SECTIONS: readonly AdminSection[] = [
  'Métricas',
  'Eventos',
  'Novedades',
  'Patrocinadores',
  'Recursos',
  'Usuarios',
  'Diagnóstico',
  'Auditoría',
  'Archivo legal',
  'Políticas',
  'Catálogos',
]

test.describe('admin access', () => {
  test('a visitor opening the events admin is sent to log in first', async ({ page }) => {
    await page.goto('/admin/events')

    await expect(page).toHaveURL(/\/login\?redirect=\/admin\/events$/)
    await expect(page.getByRole('heading', { name: 'Bienvenido de nuevo' })).toBeVisible()
  })

  test('participants and members are sent home from the administration', async ({
    pageAs,
    participant,
    member,
  }) => {
    for (const actor of [participant, member]) {
      const page = await pageAs(actor)

      await page.goto('/admin/events')

      await expect(page).toHaveURL('/')
      await expect(new AdminShell(page).heading('Eventos')).toBeHidden()
    }
  })

  test('the administration opens on the metrics dashboard', async ({ pageAs, admin }) => {
    const page = await pageAs(admin)

    await page.goto('/admin')

    await expect(page).toHaveURL(AdminShell.path('Métricas'))
    await expect(new AdminShell(page).heading('Métricas')).toBeVisible()
  })

  test('an administrator finds every section in the sidebar and a way back to the site', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const shell = new AdminShell(page)
    await shell.open('Métricas')

    await expect(shell.sidebar.getByRole('link')).toHaveText([...SECTIONS])
    await shell.link('Eventos').click()
    await expect(page).toHaveURL(AdminShell.path('Eventos'))
    await expect(shell.heading('Eventos')).toBeVisible()

    await page.getByRole('link', { name: 'Ir al sitio' }).click()
    await expect(page).toHaveURL('/')
  })

  test(
    'on a phone the sections open from the menu drawer',
    { tag: '@mobile' },
    async ({ pageAs, admin }) => {
      const page = await pageAs(admin)
      const shell = new AdminShell(page)
      await shell.open('Métricas')
      await expect(shell.sidebar).toBeHidden()

      await page.getByRole('button', { name: 'Abrir menú' }).click()

      const drawer = page.getByRole('dialog', { name: 'Administración' })
      await expect(drawer.getByRole('link')).toHaveText([...SECTIONS, 'Ir al sitio'])
      await drawer.getByRole('link', { name: 'Eventos', exact: true }).click()
      await expect(page).toHaveURL(AdminShell.path('Eventos'))
      await expect(shell.heading('Eventos')).toBeVisible()
      await expect(drawer).toBeHidden()
    },
  )
})
