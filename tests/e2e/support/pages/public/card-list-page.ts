import { expect, type Locator, type Page, type Request } from '@playwright/test'

/** A public list of cards with a search box: the news archive or the resources. */
type CardListPath = '/news' | '/resources'

const CARDS: Readonly<Record<CardListPath, string>> = {
  '/news': 'news-card',
  '/resources': 'resource-card',
}

/**
 * The news archive (`/news`) or the resources (`/resources`): a search box, the year pills of the
 * news and a grid of cards, each a link that shows the title. It records the search terms the page
 * sends to the API, so a test can tell when a search was applied.
 */
export class CardListPage {
  /** The main landmark of the page. */
  readonly main: Locator

  /** The search box. */
  readonly search: Locator

  /** The year pills, on the news archive. */
  readonly years: Locator

  /** The search terms sent to the API so far, in order. */
  readonly searches: string[] = []

  private readonly name: string

  constructor(
    private readonly page: Page,
    private readonly path: CardListPath,
  ) {
    this.name = path.slice(1)
    this.main = page.getByTestId(`page-${this.name}`)
    this.search = this.main.getByTestId(`${this.name}-search`)
    this.years = this.main.getByTestId('year-filter')
    page.on('request', (request) => {
      this.record(request)
    })
  }

  /** Opens the page and waits for the list. */
  async goto(): Promise<void> {
    await this.page.goto(this.path)
    await expect(this.main).toBeVisible()
    await expect(this.main.getByTestId(`${this.name}-loading`)).toBeHidden()
  }

  /** The card of the item titled `title`. */
  card(title: string): Locator {
    return this.main.getByTestId(CARDS[this.path]).filter({ hasText: title })
  }

  /** The pill of `year`. */
  year(year: string): Locator {
    return this.years.getByTestId('year-filter-pill').filter({ hasText: year })
  }

  /** Types `text` into the search box and applies it with Enter. */
  async searchFor(text: string): Promise<void> {
    await this.search.fill(text)
    await this.search.press('Enter')
  }

  private record(request: Request): void {
    const url = new URL(request.url())
    const term = url.searchParams.get('search')
    if (url.pathname === `/api${this.path}` && term !== null) this.searches.push(term)
  }
}
