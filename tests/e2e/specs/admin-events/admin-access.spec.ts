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
  'Archivo legal',
  'Políticas',
  'Catálogos',
]

const EDITOR_SECTIONS: readonly AdminSection[] = [
  'Eventos',
  'Novedades',
  'Patrocinadores',
  'Recursos',
  'Catálogos',
]

const PRIVACY_OFFICER_SECTIONS: readonly AdminSection[] = [
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

  test('an editor finds only the content and opens the administration on the events', async ({
    pageAs,
    editor,
  }) => {
    const page = await pageAs(editor)
    const shell = new AdminShell(page)

    await page.goto('/admin')

    await expect(page).toHaveURL(AdminShell.path('Eventos'))
    await expect(shell.sidebar.getByRole('link')).toHaveText([...EDITOR_SECTIONS])
    await page.goto(AdminShell.path('Usuarios'))
    await expect(page).toHaveURL('/')
  })

  test('the privacy officer, an administrator, also finds the audit', async ({
    pageAs,
    privacyOfficer,
  }) => {
    const page = await pageAs(privacyOfficer)
    const shell = new AdminShell(page)
    await shell.open('Métricas')

    await expect(shell.sidebar.getByRole('link')).toHaveText([...PRIVACY_OFFICER_SECTIONS])
    await shell.link('Auditoría').click()
    await expect(page).toHaveURL(AdminShell.path('Auditoría'))
    await expect(shell.heading('Auditoría')).toBeVisible()
  })

  test('an administrator other than the superadministrator is sent home from the audit', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)

    await page.goto(AdminShell.path('Auditoría'))

    await expect(page).toHaveURL('/')
  })

  test('the administration opens on the metrics dashboard', async ({ pageAs, admin }) => {
    const page = await pageAs(admin)

    await page.goto('/admin')

    await expect(page).toHaveURL(AdminShell.path('Métricas'))
    await expect(new AdminShell(page).heading('Métricas')).toBeVisible()
  })

  test('an administrator finds every section but the audit in the sidebar and a way back to the site', async ({
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
