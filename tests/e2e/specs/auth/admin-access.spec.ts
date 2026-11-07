import { expect, test } from '../../support/fixtures/test'
import { AdminShell, type AdminSection } from '../../support/pages/admin-shell'
import { SiteHeader } from '../../support/pages/site-header'

const EDITOR_SECTIONS: readonly AdminSection[] = [
  'admin-events',
  'admin-news',
  'admin-partners',
  'admin-resources',
  'admin-catalogs',
]

const ADMINISTRATOR_SECTIONS: readonly AdminSection[] = [
  'admin-dashboard',
  'admin-events',
  'admin-news',
  'admin-partners',
  'admin-resources',
  'admin-users',
  'admin-diagnostics',
  'admin-blocked-data',
  'admin-legal-policies',
  'admin-catalogs',
]

const EVERY_SECTION: readonly AdminSection[] = [...ADMINISTRATOR_SECTIONS, 'admin-audit']

test.describe('administration access', () => {
  test('a visitor opening any administration page is asked to log in first', async ({ page }) => {
    for (const section of EVERY_SECTION) {
      const path = AdminShell.path(section)

      await page.goto(path)

      await expect(page).toHaveURL(new RegExp(`/login\\?redirect=${path}$`))
    }
  })

  test('someone without a cargo is sent home from every administration page', async ({
    pageAs,
    member,
  }) => {
    const page = await pageAs(member)
    const shell = new AdminShell(page)

    for (const section of EVERY_SECTION) {
      await page.goto(AdminShell.path(section))

      await expect(page).toHaveURL('/')
      await expect(shell.view(section)).toBeHidden()
    }
    await expect(new SiteHeader(page).admin).toBeHidden()
  })

  test('an editor works in the content sections and is sent home from the rest', async ({
    pageAs,
    editor,
  }) => {
    const page = await pageAs(editor)
    const shell = new AdminShell(page)

    await page.goto('/admin')

    await expect(page).toHaveURL(AdminShell.path('admin-events'))
    await expect.poll(() => shell.sections()).toStrictEqual([...EDITOR_SECTIONS])
    await shell.expectSectionsToWork(EDITOR_SECTIONS)
    await page.goto(AdminShell.path('admin-users'))
    await expect(page).toHaveURL('/')
  })

  test('an administrator works in every section but the audit and returns to the site', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const shell = new AdminShell(page)

    await page.goto('/admin')

    await expect(page).toHaveURL(AdminShell.path('admin-dashboard'))
    await expect.poll(() => shell.sections()).toStrictEqual([...ADMINISTRATOR_SECTIONS])
    await shell.expectSectionsToWork(ADMINISTRATOR_SECTIONS)
    await page.getByTestId('admin-shell-home').click()
    await expect(page).toHaveURL('/')
    await page.goto(AdminShell.path('admin-audit'))
    await expect(page).toHaveURL('/')
  })

  test(
    'on a phone the sections open from the menu drawer',
    { tag: '@mobile' },
    async ({ pageAs, admin }) => {
      const page = await pageAs(admin)
      const shell = new AdminShell(page)
      await shell.open('admin-dashboard')
      await expect(shell.sidebar).toBeHidden()

      await page.getByTestId('admin-shell-menu').click()

      const drawer = page.getByTestId('admin-menu')
      const links = drawer.getByTestId(/^nav-/)
      await expect(links).toHaveCount(ADMINISTRATOR_SECTIONS.length)
      for (const [index, section] of ADMINISTRATOR_SECTIONS.entries()) {
        await expect(links.nth(index)).toHaveAttribute('data-testid', `nav-${section}`)
      }
      await expect(drawer.getByTestId('admin-menu-home')).toBeVisible()
      await drawer.getByTestId('nav-admin-events').click()
      await expect(page).toHaveURL(AdminShell.path('admin-events'))
      await expect(shell.view('admin-events')).toBeVisible()
      await expect(drawer).toBeHidden()
    },
  )
})
