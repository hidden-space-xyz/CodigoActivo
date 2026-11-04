import { expect, type Locator, type Page } from '@playwright/test'

import { chooseOption } from '../../ui/element-plus'

/**
 * The public events page, at `/events`: the upcoming events, loaded 25 at a time, and the past
 * ones of the chosen year, filtered by text and category.
 */
export class EventsPage {
  /** The «Próximos eventos» section. */
  readonly upcoming: Locator

  /** The cards of the upcoming events loaded so far. */
  readonly upcomingCards: Locator

  /** The button that loads the next upcoming events. */
  readonly loadMore: Locator

  /** The «Eventos anteriores» section. */
  readonly past: Locator

  /** The year pills of the past events. */
  readonly years: Locator

  /** The search box of the past events. */
  readonly search: Locator

  /** The category select of the past events. */
  readonly category: Locator

  constructor(private readonly page: Page) {
    const main = page.getByRole('main')
    this.upcoming = this.section(main, 'Próximos eventos')
    this.upcomingCards = this.upcoming.locator('.board').getByRole('link')
    this.loadMore = this.upcoming.getByRole('button', { name: 'Cargar más' })
    this.past = this.section(main, 'Eventos anteriores')
    this.years = this.past.getByRole('group', { name: 'Filtrar por año' })
    this.search = this.past.getByRole('textbox', { name: 'Buscar por título o subtítulo' })
    this.category = this.past.getByRole('combobox', { name: 'Filtrar por categoría' })
  }

  /** Opens the page and waits for the upcoming events. */
  async goto(): Promise<void> {
    await this.page.goto('/events')
    await expect(this.upcoming.getByText('Cargando…')).toBeHidden()
  }

  /** The card of the event titled `title` inside `section`. */
  card(section: Locator, title: string): Locator {
    return section
      .getByRole('link')
      .filter({ has: this.page.getByRole('heading', { level: 3, name: title, exact: true }) })
  }

  /** The pill of `year` among the past events. */
  year(year: string | number): Locator {
    return this.years.getByRole('button', { name: String(year), exact: true })
  }

  /** Loads more upcoming events, as a visitor scrolling down would, until `title` is listed. */
  async revealUpcoming(title: string): Promise<Locator> {
    const card = this.card(this.upcoming, title)
    while ((await card.count()) === 0 && (await this.loadMore.isVisible())) {
      const loaded = await this.upcomingCards.count()
      await this.loadMore.click()
      await expect(this.upcomingCards).not.toHaveCount(loaded)
    }
    return card
  }

  /** Searches the past events for `text`, applied with Enter. */
  async searchPast(text: string): Promise<void> {
    await this.search.fill(text)
    await this.search.press('Enter')
  }

  /** Picks the category `name` in the past events filter. */
  async filterPastBy(name: string): Promise<void> {
    await chooseOption(this.category, name)
  }

  private section(main: Locator, title: string): Locator {
    return main
      .locator('section')
      .filter({ has: this.page.getByRole('heading', { level: 2, name: title, exact: true }) })
  }
}
