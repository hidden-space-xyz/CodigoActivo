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
    const main = page.getByTestId('page-events')
    this.upcoming = main.getByTestId('events-upcoming')
    this.upcomingCards = this.upcoming.getByTestId('event-card')
    this.loadMore = this.upcoming.getByTestId('events-more')
    this.past = main.getByTestId('events-past')
    this.years = this.past.getByTestId('year-filter')
    this.search = this.past.getByTestId('events-search')
    this.category = this.past.getByTestId('event-category-filter')
  }

  /** Opens the page and waits for the upcoming events. */
  async goto(): Promise<void> {
    await this.page.goto('/events')
    await expect(this.upcoming).toBeVisible()
    await expect(this.upcoming.getByTestId('events-loading')).toBeHidden()
  }

  /** The card of the event titled `title` inside `section`. */
  card(section: Locator, title: string): Locator {
    return section.getByTestId('event-card').filter({ hasText: title })
  }

  /** The pill of `year` among the past events. */
  year(year: string | number): Locator {
    return this.years.getByTestId('year-filter-pill').filter({ hasText: String(year) })
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

  /** Picks the category with the identifier `categoryId` in the past events filter. */
  async filterPastBy(categoryId: string): Promise<void> {
    await chooseOption(this.category, categoryId)
  }
}
