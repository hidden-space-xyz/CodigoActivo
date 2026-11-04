import { expect, test } from '../../support/fixtures/test'
import { AdminShell, type AdminSection } from '../../support/pages/admin-shell'

const SECTIONS: readonly AdminSection[] = [
  'Novedades',
  'Patrocinadores',
  'Recursos',
  'Catálogos',
  'Políticas',
]

test.describe('content administration access', () => {
  test('an administrator reaches every content page from the sidebar', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const shell = new AdminShell(page)
    await shell.open('Métricas')

    for (const section of SECTIONS) {
      await shell.link(section).click()
      await expect(page).toHaveURL(AdminShell.path(section))
      await expect(shell.heading(section)).toBeVisible()
    }
  })

  for (const section of SECTIONS) {
    const path = AdminShell.path(section)

    test(`a participant opening ${path} is sent to the home page`, async ({
      pageAs,
      participant,
    }) => {
      const page = await pageAs(participant)

      await page.goto(path)

      await expect(page).toHaveURL('/')
      await expect(new AdminShell(page).heading(section)).toBeHidden()
    })

    test(`a visitor opening ${path} is asked to log in first`, async ({ page }) => {
      await page.goto(path)

      await expect(page).toHaveURL(new RegExp(`/login\\?redirect=${path}$`))
    })
  }
})
