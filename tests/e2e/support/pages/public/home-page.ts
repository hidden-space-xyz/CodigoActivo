import { expect, type Locator, type Page } from '@playwright/test'

/** A section of the home page that previews a list: «Novedades» or «Eventos». */
class HomeSection {
  /** The section, found by its heading. */
  readonly root: Locator

  /** The link to the whole list, such as «Ver todos los eventos →». */
  readonly viewAll: Locator

  /** The large card of the featured item, when there is one. */
  readonly featured: Locator

  constructor(
    private readonly page: Page,
    title: string,
    viewAll: string,
  ) {
    this.root = page
      .getByRole('main')
      .locator('section')
      .filter({ has: page.getByRole('heading', { level: 2, name: title, exact: true }) })
    this.viewAll = this.root.getByRole('link', { name: viewAll, exact: true })
    this.featured = this.root.locator('.featured')
  }

  /** The card of the item titled `title` in the grid under the featured one. */
  card(title: string): Locator {
    return this.root
      .locator('.home-section__grid')
      .getByRole('link')
      .filter({ has: this.page.getByRole('heading', { level: 3, name: title, exact: true }) })
  }
}

/**
 * The «Con el apoyo de» carousel: the partners in a ring, the active one in the middle with its
 * name under the logo, the arrows that move it and the timer that advances it every three seconds
 * while the pointer is not on a partner.
 */
class SponsorCarousel {
  /** The section of the carousel. */
  readonly root: Locator

  /** The «Con el apoyo de» heading. */
  readonly heading: Locator

  /** The arrow that moves back to the previous partner. */
  readonly previous: Locator

  /** The arrow that moves on to the next partner. */
  readonly next: Locator

  /** The card of the partner in the middle. */
  readonly active: Locator

  /** The name shown under the logo of the partner in the middle. */
  readonly activeName: Locator

  constructor(page: Page) {
    this.root = page.locator('section.sponsors')
    this.heading = this.root.getByText('Con el apoyo de', { exact: true })
    this.previous = this.root.getByRole('button', { name: 'Patrocinador anterior' })
    this.next = this.root.getByRole('button', { name: 'Patrocinador siguiente' })
    this.active = this.root.locator('.sponsor--d0')
    this.activeName = this.active.locator('.sponsor__name')
  }

  /** The logo of the partner `name`, whose alternative text is the name. */
  logo(name: string): Locator {
    return this.root.getByRole('img', { name, exact: true })
  }

  /** The link to the website of the partner `name`, for partners that have one. */
  link(name: string): Locator {
    return this.root.getByRole('link', { name })
  }

  /** The name of the partner in the middle. */
  async activePartner(): Promise<string> {
    await expect(this.activeName).toBeVisible()
    return (await this.activeName.innerText()).trim()
  }

  /** Moves on with the arrow, at most twice round the ring, until `name` is in the middle. */
  async show(name: string): Promise<void> {
    const steps = 2 * (await this.root.locator('.sponsor').count())
    for (let step = 0; step < steps && (await this.activePartner()) !== name; step += 1) {
      await this.next.click()
    }
    await expect(this.activeName).toHaveText(name)
  }
}

/** The home page: the hero, the news and events previews and the sponsors carousel. */
export class HomePage {
  /** The main landmark of the page. */
  readonly main: Locator

  /** The `h1` of the hero. */
  readonly title: Locator

  /** The preview of the latest news. */
  readonly news: HomeSection

  /** The preview of the upcoming events. */
  readonly events: HomeSection

  /** The sponsors carousel. */
  readonly sponsors: SponsorCarousel

  constructor(private readonly page: Page) {
    this.main = page.getByRole('main')
    this.title = this.main.getByRole('heading', { level: 1 })
    this.news = new HomeSection(page, 'Novedades', 'Ver todas las novedades →')
    this.events = new HomeSection(page, 'Eventos', 'Ver todos los eventos →')
    this.sponsors = new SponsorCarousel(page)
  }

  /** Opens the home page and waits for the hero. */
  async goto(): Promise<void> {
    await this.page.goto('/')
    await expect(this.title).toBeVisible()
  }
}
