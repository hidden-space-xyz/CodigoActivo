import { expect, test } from '../../support/fixtures/test'
import { MenuDrawer, SiteTheme } from '../../support/pages/public/site-chrome'
import { SiteHeader } from '../../support/pages/site-header'

const SECTIONS = [
  { link: 'Inicio', path: '/' },
  { link: 'Novedades', path: '/news' },
  { link: 'Eventos', path: '/events' },
  { link: 'Recursos', path: '/resources' },
  { link: 'Nosotros', path: '/about' },
] as const

const LEGAL_EMAIL = 'contacto@e2e.codigoactivo.test'
const LEGAL_PHONE = '600 00 00 00'

test.describe('site navigation', () => {
  test('the header links to every section and marks the current one', async ({ page }) => {
    const header = new SiteHeader(page)
    await page.goto('/about')

    for (const section of SECTIONS) {
      const link = header.navigation.getByRole('link', { name: section.link, exact: true })
      await link.click()
      await expect(page).toHaveURL(section.path)
      await expect(link).toHaveAttribute('aria-current', 'page')
      for (const other of SECTIONS.filter((candidate) => candidate !== section)) {
        await expect(
          header.navigation.getByRole('link', { name: other.link, exact: true }),
        ).not.toHaveAttribute('aria-current', 'page')
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

  test('signing out from the header turns the person back into a visitor', async ({
    pageAs,
    accounts,
  }) => {
    const actor = await accounts.actor({ label: 'header-logout' })
    const page = await pageAs(actor)
    const header = new SiteHeader(page)
    await page.goto('/news')
    await expect(header.greeting(actor.account.firstName)).toBeVisible()

    await header.logout.click()

    await expect(page).toHaveURL('/login')
    await expect(header.login).toBeVisible()
    await expect(header.account).toBeHidden()
    await expect(header.greeting(actor.account.firstName)).toBeHidden()
  })

  test('the footer links to the legal pages', async ({ page }) => {
    const footer = page.getByRole('contentinfo')
    const legal = footer.getByRole('navigation', { name: 'Información legal' })
    await page.goto('/')

    await legal.getByRole('link', { name: 'Política de privacidad' }).click()
    await expect(page).toHaveURL('/privacy')
    await expect(
      page.getByRole('heading', { level: 1, name: 'Política de privacidad' }),
    ).toBeVisible()
    await legal.getByRole('link', { name: 'Política de cookies' }).click()
    await expect(page).toHaveURL('/cookies')
    await expect(page.getByRole('heading', { level: 1, name: 'Política de cookies' })).toBeVisible()
  })

  test('the footer gives the contact details and the social channels', async ({ page }) => {
    const footer = page.getByRole('contentinfo')
    await page.goto('/')

    await expect(footer.getByRole('link', { name: LEGAL_EMAIL })).toHaveAttribute(
      'href',
      `mailto:${LEGAL_EMAIL}`,
    )
    await expect(footer.getByRole('link', { name: LEGAL_PHONE })).toHaveAttribute(
      'href',
      'tel:600000000',
    )
    for (const [name, url] of [
      ['Instagram', 'https://www.instagram.com/codigoactivo_'],
      ['Facebook', 'https://www.facebook.com/codigoactivo'],
      ['LinkedIn', 'https://www.linkedin.com/company/codigoactivo'],
      ['YouTube', 'https://www.youtube.com/@campeonatocodigoactivo9489'],
    ] as const) {
      const link = footer.getByRole('link', { name, exact: true })
      await expect(link).toHaveAttribute('href', url)
      await expect(link).toHaveAttribute('target', '_blank')
      await expect(link).toHaveAttribute('rel', 'noopener')
    }
  })

  test('the theme toggle switches to the dark theme and remembers it', async ({ page }) => {
    const header = new SiteHeader(page)
    const theme = new SiteTheme(page)
    await page.goto('/')
    await expect(header.themeToggle).toHaveAccessibleName('Cambiar a tema oscuro')
    await expect(header.themeToggle).toHaveAttribute('aria-pressed', 'false')
    await expect(theme.root).not.toContainClass('ca-dark')

    await header.themeToggle.click()

    await expect(header.themeToggle).toHaveAccessibleName('Cambiar a tema claro')
    await expect(header.themeToggle).toHaveAttribute('aria-pressed', 'true')
    await expect(theme.root).toContainClass('ca-dark')
    await expect.poll(() => theme.saved()).toBe('dark')
    await page.reload()
    await expect(theme.root).toContainClass('ca-dark')
    await expect(header.themeToggle).toHaveAttribute('aria-pressed', 'true')

    await header.themeToggle.click()

    await expect(theme.root).not.toContainClass('ca-dark')
    await expect(header.themeToggle).toHaveAccessibleName('Cambiar a tema oscuro')
    await expect.poll(() => theme.saved()).toBe('light')
  })

  test('an unknown address shows the not found page', async ({ page }) => {
    await page.goto('/no-existe/esta-pagina')

    await expect(
      page.getByRole('heading', { level: 1, name: 'Página no encontrada' }),
    ).toBeVisible()
    await page.getByRole('main').getByRole('link', { name: 'Volver al inicio' }).click()
    await expect(page).toHaveURL('/')
  })

  test('detail pages only take identifiers in GUID form', async ({ page }) => {
    for (const path of ['/events/1', '/news/novedad', '/resources/not-a-guid']) {
      await page.goto(path)
      await expect(
        page.getByRole('heading', { level: 1, name: 'Página no encontrada' }),
      ).toBeVisible()
    }
  })

  test('the about page presents the association and how to join it', async ({ page }) => {
    await page.goto('/about')
    const main = page.getByRole('main')

    await expect(main.getByRole('heading', { level: 1 })).toContainText('sea de tod@s')
    for (const [value, description] of [
      ['Gratuito', 'Todas nuestras actividades son siempre sin coste para las familias.'],
      ['Inclusivo', 'Llevamos el código al mundo rural y a quien tiene menos acceso.'],
      ['En comunidad', 'Voluntariado, familias y empresas que reman en la misma dirección.'],
      ['Divertido', 'Se aprende creando, jugando y compartiendo proyectos propios.'],
    ] as const) {
      await expect(main.getByText(value, { exact: true })).toBeVisible()
      await expect(main.getByText(description, { exact: true })).toBeVisible()
    }
    await main.getByRole('button', { name: 'Únete', exact: true }).click()

    const join = page.getByRole('dialog', { name: 'Únete a Código Activo' })
    await expect(join.getByRole('link', { name: LEGAL_EMAIL })).toHaveAttribute(
      'href',
      `mailto:${LEGAL_EMAIL}`,
    )
    await expect(join.getByRole('link', { name: LEGAL_PHONE })).toHaveAttribute(
      'href',
      'tel:600000000',
    )
  })

  test('on a phone the navigation lives in the menu drawer @mobile', async ({ page }) => {
    const header = new SiteHeader(page)
    const menu = new MenuDrawer(page)
    await page.goto('/')
    await expect(header.navigation.getByRole('link', { name: 'Eventos' })).toBeHidden()
    await expect(header.login).toBeHidden()

    await header.menuButton.click()

    await expect(menu.root).toBeVisible()
    for (const section of SECTIONS) {
      await expect(menu.link(section.link)).toBeVisible()
    }
    await expect(menu.link('Regístrate')).toBeVisible()
    await expect(menu.link('Iniciar sesión')).toBeVisible()
    await menu.link('Eventos').click()
    await expect(page).toHaveURL('/events')
    await expect(menu.root).toBeHidden()
    await header.menuButton.click()
    await menu.link('Iniciar sesión').click()
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

    await expect(menu.root.getByText(`Hola, ${participant.account.firstName}`)).toBeVisible()
    await expect(menu.root.getByRole('button', { name: 'Cerrar sesión' })).toBeVisible()
    await expect(menu.link('Administración')).toBeHidden()
    await menu.link('Mi cuenta').click()
    await expect(page).toHaveURL('/account')
    await expect(menu.root).toBeHidden()
  })
})
