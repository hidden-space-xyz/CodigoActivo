import { expect, test } from '../../support/fixtures/test'
import { AdminShell, type AdminSection } from '../../support/pages/admin-shell'

const SECTIONS: readonly AdminSection[] = [
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

const EDITOR_SECTIONS: readonly AdminSection[] = [
  'admin-events',
  'admin-news',
  'admin-partners',
  'admin-resources',
  'admin-catalogs',
]

const PRIVACY_OFFICER_SECTIONS: readonly AdminSection[] = [
  'admin-dashboard',
  'admin-events',
  'admin-news',
  'admin-partners',
  'admin-resources',
  'admin-users',
  'admin-diagnostics',
  'admin-audit',
  'admin-blocked-data',
  'admin-legal-policies',
  'admin-catalogs',
]

test.describe('admin access', () => {
  test('a visitor opening the events admin is sent to log in first', async ({ page }) => {
    await page.goto('/admin/events')

    await expect(page).toHaveURL(/\/login\?redirect=\/admin\/events$/)
    await expect(page.getByTestId('page-login')).toBeVisible()
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
      await expect(new AdminShell(page).view('admin-events')).toBeHidden()
    }
  })

  test('an editor finds only the content and opens the administration on the events', async ({
    pageAs,
    editor,
  }) => {
    const page = await pageAs(editor)
    const shell = new AdminShell(page)

    await page.goto('/admin')

    await expect(page).toHaveURL(AdminShell.path('admin-events'))
    await expect.poll(() => shell.sections()).toStrictEqual([...EDITOR_SECTIONS])
    await page.goto(AdminShell.path('admin-users'))
    await expect(page).toHaveURL('/')
  })

  test('the privacy officer, an administrator, also finds the audit', async ({
    pageAs,
    privacyOfficer,
  }) => {
    const page = await pageAs(privacyOfficer)
    const shell = new AdminShell(page)
    await shell.open('admin-dashboard')

    await expect.poll(() => shell.sections()).toStrictEqual([...PRIVACY_OFFICER_SECTIONS])
    await shell.link('admin-audit').click()
    await expect(page).toHaveURL(AdminShell.path('admin-audit'))
    await expect(shell.view('admin-audit')).toBeVisible()
  })

  test('an administrator other than the superadministrator is sent home from the audit', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)

    await page.goto(AdminShell.path('admin-audit'))

    await expect(page).toHaveURL('/')
  })

  test('the administration opens on the metrics dashboard', async ({ pageAs, admin }) => {
    const page = await pageAs(admin)

    await page.goto('/admin')

    await expect(page).toHaveURL(AdminShell.path('admin-dashboard'))
    await expect(new AdminShell(page).view('admin-dashboard')).toBeVisible()
  })

  test('an administrator finds every section but the audit in the sidebar and a way back to the site', async ({
    pageAs,
    admin,
  }) => {
    const page = await pageAs(admin)
    const shell = new AdminShell(page)
    await shell.open('admin-dashboard')

    await expect.poll(() => shell.sections()).toStrictEqual([...SECTIONS])
    await shell.link('admin-events').click()
    await expect(page).toHaveURL(AdminShell.path('admin-events'))
    await expect(shell.view('admin-events')).toBeVisible()

    await page.getByTestId('admin-shell-home').click()
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
      await expect(links).toHaveCount(SECTIONS.length)
      for (const [index, section] of SECTIONS.entries()) {
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
