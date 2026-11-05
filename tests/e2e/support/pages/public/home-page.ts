import { expect, type Locator, type Page } from '@playwright/test'

/** A section of the home page that previews a list: «Novedades» or «Eventos». */
class HomeSection {
  /** The section that holds the heading, the link to the whole list and the cards. */
  readonly root: Locator

  /** The link to the whole list, such as «Ver todos los eventos →». */
  readonly viewAll: Locator

  /** The large card of the featured item, when there is one. */
  readonly featured: Locator

  constructor(
    page: Page,
    section: string,
    private readonly cardTestId: string,
  ) {
    this.root = page.getByTestId(section)
    this.viewAll = this.root.getByTestId('home-section-view-all')
    this.featured = this.root.getByTestId('featured-card')
  }

  /** The card of the item titled `title` in the grid under the featured one. */
  card(title: string): Locator {
    return this.root.getByTestId(this.cardTestId).filter({ hasText: title })
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

  /** The card of the partner in the middle, the only one that shows its name. */
  readonly active: Locator

  /** The name shown under the logo of the partner in the middle. */
  readonly activeName: Locator

  private readonly partners: Locator

  constructor(private readonly page: Page) {
    this.root = page.getByTestId('home-sponsors')
    this.heading = this.root.getByTestId('home-sponsors-heading')
    this.previous = this.root.getByTestId('home-sponsors-previous')
    this.next = this.root.getByTestId('home-sponsors-next')
    this.partners = this.root.getByTestId('home-sponsor')
    this.activeName = this.root.getByTestId('home-sponsor-name')
    this.active = this.partners.filter({ has: page.getByTestId('home-sponsor-name') })
  }

  /** The logo of the partner `name` while it is in the middle, where its name shows. */
  logo(name: string): Locator {
    return this.partner(name).getByTestId('home-sponsor-logo')
  }

  /**
   * The link to the website of the partner `name` while it is in the middle, for partners that
   * have one: the card of the partner is then a link instead of a plain box.
   */
  link(name: string): Locator {
    return this.partner(name).and(this.root.locator('a'))
  }

  /** The name of the partner in the middle. */
  async activePartner(): Promise<string> {
    await expect(this.activeName).toBeVisible()
    return (await this.activeName.innerText()).trim()
  }

  /** Moves on with the arrow, at most twice round the ring, until `name` is in the middle. */
  async show(name: string): Promise<void> {
    const steps = 2 * (await this.partners.count())
    for (let step = 0; step < steps && (await this.activePartner()) !== name; step += 1) {
      await this.next.click()
    }
    await expect(this.activeName).toHaveText(name)
  }

  private partner(name: string): Locator {
    return this.partners.filter({
      has: this.page.getByTestId('home-sponsor-name').filter({ hasText: name }),
    })
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
    this.main = page.getByTestId('page-home')
    this.title = this.main.getByTestId('home-hero-title')
    this.news = new HomeSection(page, 'home-news', 'news-card')
    this.events = new HomeSection(page, 'home-events', 'event-card')
    this.sponsors = new SponsorCarousel(page)
  }

  /** Opens the home page and waits for the hero. */
  async goto(): Promise<void> {
    await this.page.goto('/')
    await expect(this.title).toBeVisible()
  }
}
