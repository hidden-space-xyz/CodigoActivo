import { PublicEvents } from '../../support/api/public'
import { uniqueLetters } from '../../support/data/unique'
import { expect, test } from '../../support/fixtures/test'
import { cardDate, dayRangeText, dayText } from '../../support/data/formats'
import { t } from '../../support/i18n'
import { HomePage } from '../../support/pages/public/home-page'

const SLOGAN = `${t('pages.home.hero.titleLine2')} ${t('pages.home.hero.titleHighlight')}`

test.describe('home page', () => {
  let events: PublicEvents

  test.beforeEach(({ admin, seed }) => {
    events = new PublicEvents(admin.api, seed)
  })

  test.afterEach(async () => {
    await events.dispose()
  })

  test('the hero invites visitors to register and to meet the association', async ({ page }) => {
    const home = new HomePage(page)
    await home.goto()

    await expect(home.title).toContainText(SLOGAN)
    await expect(home.main.getByTestId('home-hero-events')).toBeHidden()
    await home.main.getByTestId('home-hero-register').click()
    await expect(page).toHaveURL('/register')
    await home.goto()
    await home.main.getByTestId('home-hero-about').click()
    await expect(page).toHaveURL('/about')
  })

  test('the hero sends signed-in people to the events', async ({ pageAs, participant }) => {
    const page = await pageAs(participant)
    const home = new HomePage(page)
    await home.goto()

    await expect(home.title).toContainText(SLOGAN)
    await expect(home.main.getByTestId('home-hero-register')).toBeHidden()
    await home.main.getByTestId('home-hero-events').click()
    await expect(page).toHaveURL('/events')
  })

  test('the news section previews the latest news and leads to the archive', async ({
    page,
    seed,
  }) => {
    const item = await seed.newsItem()
    const home = new HomePage(page)
    await home.goto()

    const card = home.news.card(item.title)
    await expect(card.getByTestId('card-date')).toHaveText(
      cardDate(t('entities.newsItem.card.dateLabel'), dayText(item.createdAt)),
    )
    await expect(card).toContainText(item.subtitle)
    const thumbnail = card.getByTestId('list-thumbnail-image')
    await expect(thumbnail).toBeVisible()
    await expect(thumbnail).toHaveAccessibleName(item.title)
    await card.click()
    await expect(page).toHaveURL(`/news/${item.id}`)
    await home.goto()
    await home.news.viewAll.click()
    await expect(page).toHaveURL('/news')
  })

  test('the events section previews the first upcoming events with their signup status', async ({
    page,
    seed,
  }) => {
    const category = await seed.category()
    const open = await events.running('open', { categories: [category] })
    const soon = await events.running('upcoming', { categories: [category] })
    const early = await events.running('early', { categories: [category] })
    const home = new HomePage(page)
    await home.goto()

    for (const [event, status] of [
      [open, t('entities.event.status.signupOpen')],
      [soon, t('entities.event.status.upcoming')],
      [early, t('entities.event.status.earlySignupOpen')],
    ] as const) {
      const card = home.events.card(event.title)
      await expect(card).toHaveAttribute('href', `/events/${event.id}`)
      await expect(card.getByTestId('card-date')).toHaveText(
        cardDate(
          t('entities.event.card.dateLabel'),
          dayRangeText(event.eventStartsAt, event.eventEndsAt),
        ),
      )
      await expect(card).toContainText(status)
      await expect(card).toContainText(category.name)
    }
    await home.events.card(open.title).click()
    await expect(page).toHaveURL(`/events/${open.id}`)
    await home.goto()
    await home.events.viewAll.click()
    await expect(page).toHaveURL('/events')
  })

  test('each sponsor shows its logo and links to its website', async ({ page, seed }) => {
    const website = `https://${uniqueLetters()}.partner.e2e.codigoactivo.test/`
    const withWebsite = await seed.partner({ tier: 0, website })
    const withoutWebsite = await seed.partner({ tier: 0 })
    const home = new HomePage(page)
    await home.goto()
    await expect(home.sponsors.heading).toBeVisible()

    await home.sponsors.show(withWebsite.name)
    await expect(home.sponsors.logo(withWebsite.name)).toBeVisible()
    await expect(home.sponsors.link(withWebsite.name)).toHaveAttribute('href', website)
    await expect(home.sponsors.link(withWebsite.name)).toHaveAttribute('target', '_blank')

    await home.sponsors.show(withoutWebsite.name)
    await expect(home.sponsors.logo(withoutWebsite.name)).toBeVisible()
    await expect(home.sponsors.link(withoutWebsite.name)).toHaveCount(0)
  })

  test('the arrows move to the next and the previous sponsor', async ({ page, seed }) => {
    await seed.partner({ tier: 0 })
    await seed.partner({ tier: 0 })
    await page.clock.install()
    const home = new HomePage(page)
    await home.goto()
    await expect(home.sponsors.activeName).toBeVisible()
    await page.clock.pauseAt(Date.now() + 1_000)
    const first = await home.sponsors.activePartner()

    await home.sponsors.next.click()
    await expect(home.sponsors.activeName).not.toHaveText(first)
    await home.sponsors.previous.click()
    await expect(home.sponsors.activeName).toHaveText(first)
  })

  test('the sponsors advance every three seconds unless the pointer rests on one', async ({
    page,
    seed,
  }) => {
    await seed.partner({ tier: 0 })
    await seed.partner({ tier: 0 })
    await page.clock.install()
    const home = new HomePage(page)
    await home.goto()
    await expect(home.sponsors.activeName).toBeVisible()
    await page.clock.pauseAt(Date.now() + 1_000)
    await home.sponsors.next.click()
    const shown = await home.sponsors.activePartner()

    await page.clock.runFor(2_900)
    await expect(home.sponsors.activeName).toHaveText(shown)
    await page.clock.runFor(100)
    await expect(home.sponsors.activeName).not.toHaveText(shown)

    const hovered = await home.sponsors.activePartner()
    await home.sponsors.active.hover()
    await page.clock.runFor(9_000)
    await expect(home.sponsors.activeName).toHaveText(hovered)
  })
})
