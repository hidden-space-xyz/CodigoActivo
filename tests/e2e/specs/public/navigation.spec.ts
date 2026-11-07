import { LEGAL_ENTITY } from '../../support/config/environment'
import { expect, test } from '../../support/fixtures/test'
import { t } from '../../support/i18n'
import { MenuDrawer, SiteTheme } from '../../support/pages/public/site-chrome'
import { SiteHeader } from '../../support/pages/site-header'

const SECTIONS = [
  { section: 'home', path: '/' },
  { section: 'news', path: '/news' },
  { section: 'events', path: '/events' },
  { section: 'resources', path: '/resources' },
  { section: 'about', path: '/about' },
] as const

test.describe('site navigation', () => {
  test('the header links to every section and marks the current one', async ({ page }) => {
    const header = new SiteHeader(page)
    await page.goto('/about')

    for (const section of SECTIONS) {
      const link = header.link(section.section)
      await link.click()
      await expect(page).toHaveURL(section.path)
      await expect(link).toHaveAttribute('aria-current', 'page')
      for (const other of SECTIONS.filter((candidate) => candidate !== section)) {
        await expect(header.link(other.section)).not.toHaveAttribute('aria-current', 'page')
      }
    }
  })

  test('visitors are offered to sign in or to register', async ({ page }) => {
    const header = new SiteHeader(page)
    await page.goto('/')

    await expect(header.account).toBeHidden()
    await expect(header.logout).toBeHidden()
    await expect(header.admin).toBeHidden()
    await header.login.click()
    await expect(page).toHaveURL('/login')
    await page.goto('/')
    await header.register.click()
    await expect(page).toHaveURL('/register')
  })

  test('a participant reaches their account but not the administration', async ({
    pageAs,
    participant,
  }) => {
    const page = await pageAs(participant)
    const header = new SiteHeader(page)
    await page.goto('/')

    await expect(header.greeting(participant.account.firstName)).toBeVisible()
    await expect(header.logout).toBeVisible()
    await expect(header.login).toBeHidden()
    await expect(header.register).toBeHidden()
    await expect(header.admin).toBeHidden()
    await header.account.click()
    await expect(page).toHaveURL('/account')
    await expect(header.account).toHaveAttribute('aria-current', 'page')
  })

  test('an administrator also reaches the administration', async ({ pageAs, admin }) => {
    const page = await pageAs(admin)
    const header = new SiteHeader(page)
    await page.goto('/')

    await expect(header.greeting(admin.account.firstName)).toBeVisible()
    await expect(header.account).toBeVisible()
    await header.admin.click()
    await expect(page).toHaveURL('/admin/dashboard')
  })

  test('the footer links to the legal pages', async ({ page }) => {
    const footer = page.getByTestId('site-footer')
    await page.goto('/')

    await footer.getByTestId('nav-privacy-policy').click()
    await expect(page).toHaveURL('/privacy')
    await expect(page.getByTestId('page-privacy-policy').getByTestId('page-head-title')).toHaveText(
      t('pages.privacyPolicy.title'),
    )
    await footer.getByTestId('nav-cookie-policy').click()
    await expect(page).toHaveURL('/cookies')
    await expect(page.getByTestId('page-cookie-policy').getByTestId('page-head-title')).toHaveText(
      t('pages.cookiePolicy.title'),
    )
  })

  test('the footer gives the contact details and the social channels', async ({ page }) => {
    const footer = page.getByTestId('site-footer')
    await page.goto('/')

    const email = footer.getByTestId('site-footer-email')
    await expect(email).toHaveText(LEGAL_ENTITY.email)
    await expect(email).toHaveAttribute('href', `mailto:${LEGAL_ENTITY.email}`)
    const phone = footer.getByTestId('site-footer-phone')
    await expect(phone).toHaveText(LEGAL_ENTITY.phone)
    await expect(phone).toHaveAttribute('href', LEGAL_ENTITY.phoneLink)
    for (const [channel, url] of [
      ['instagram', 'https://www.instagram.com/codigoactivo_'],
      ['facebook', 'https://www.facebook.com/codigoactivo'],
      ['linkedin', 'https://www.linkedin.com/company/codigoactivo'],
      ['youtube', 'https://www.youtube.com/@campeonatocodigoactivo9489'],
    ] as const) {
      const link = footer.getByTestId(`site-footer-${channel}`)
      await expect(link).toHaveAttribute('href', url)
      await expect(link).toHaveAttribute('target', '_blank')
      await expect(link).toHaveAttribute('rel', 'noopener')
    }
  })

  test('the theme toggle switches to the dark theme and remembers it', async ({ page }) => {
    const header = new SiteHeader(page)
    const theme = new SiteTheme(page)
    await page.goto('/')
    await expect(header.themeToggle).toHaveAccessibleName(t('theme.toDark'))
    await expect(header.themeToggle).toHaveAttribute('aria-pressed', 'false')
    await expect(theme.root).not.toContainClass('ca-dark')

    await header.themeToggle.click()

    await expect(header.themeToggle).toHaveAccessibleName(t('theme.toLight'))
    await expect(header.themeToggle).toHaveAttribute('aria-pressed', 'true')
    await expect(theme.root).toContainClass('ca-dark')
    await expect.poll(() => theme.saved()).toBe('dark')
    await page.reload()
    await expect(theme.root).toContainClass('ca-dark')
    await expect(header.themeToggle).toHaveAttribute('aria-pressed', 'true')

    await header.themeToggle.click()

    await expect(theme.root).not.toContainClass('ca-dark')
    await expect(header.themeToggle).toHaveAccessibleName(t('theme.toDark'))
    await expect.poll(() => theme.saved()).toBe('light')
  })

  test('an unknown address shows the not found page', async ({ page }) => {
    await page.goto('/no-existe/esta-pagina')

    const main = page.getByTestId('page-not-found')
    await expect(main.getByTestId('page-head-title')).toHaveText(t('pages.notFound.title'))
    await main.getByTestId('not-found-home').click()
    await expect(page).toHaveURL('/')
  })

  test('detail pages only take identifiers in GUID form', async ({ page }) => {
    for (const path of ['/events/1', '/news/novedad', '/resources/not-a-guid']) {
      await page.goto(path)
      await expect(page.getByTestId('page-not-found').getByTestId('page-head-title')).toHaveText(
        t('pages.notFound.title'),
      )
    }
  })

  test('the about page tells how to contact the association to join it', async ({ page }) => {
    await page.goto('/about')

    await page.getByTestId('page-about').getByTestId('about-join').click()

    const join = page.getByTestId('about-join-dialog')
    const email = join.getByTestId('about-join-email')
    await expect(email).toHaveText(LEGAL_ENTITY.email)
    await expect(email).toHaveAttribute('href', `mailto:${LEGAL_ENTITY.email}`)
    const phone = join.getByTestId('about-join-phone')
    await expect(phone).toHaveText(LEGAL_ENTITY.phone)
    await expect(phone).toHaveAttribute('href', LEGAL_ENTITY.phoneLink)
  })

  test('on a phone the navigation lives in the menu drawer @mobile', async ({ page }) => {
    const header = new SiteHeader(page)
    const menu = new MenuDrawer(page)
    await page.goto('/')
    await expect(header.link('events')).toBeHidden()
    await expect(header.login).toBeHidden()

    await header.menuButton.click()

    await expect(menu.root).toBeVisible()
    for (const section of SECTIONS) {
      await expect(menu.link(section.section)).toBeVisible()
    }
    await expect(menu.register).toBeVisible()
    await expect(menu.login).toBeVisible()
    await menu.link('events').click()
    await expect(page).toHaveURL('/events')
    await expect(menu.root).toBeHidden()
    await header.menuButton.click()
    await menu.login.click()
    await expect(page).toHaveURL('/login')
    await expect(menu.root).toBeHidden()
  })

  test('on a phone signed-in people find their account in the menu drawer @mobile', async ({
    pageAs,
    participant,
  }) => {
    const page = await pageAs(participant)
    const header = new SiteHeader(page)
    const menu = new MenuDrawer(page)
    await page.goto('/')

    await header.menuButton.click()

    await expect(menu.greeting).toHaveText(
      t('common.greeting', { name: participant.account.firstName }),
    )
    await expect(menu.logout).toBeVisible()
    await expect(menu.admin).toBeHidden()
    await menu.account.click()
    await expect(page).toHaveURL('/account')
    await expect(menu.root).toBeHidden()
  })
})
