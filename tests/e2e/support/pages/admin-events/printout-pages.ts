import { expect, type Locator, type Page } from '@playwright/test'

/**
 * Replaces `window.print` in every document `page` opens from now on with a counter kept in the
 * `data-print-calls` attribute of the root element, so a test can tell the print dialog was asked
 * for without opening it. Call it before the first navigation.
 */
export async function stubPrint(page: Page): Promise<void> {
  await page.addInitScript(() => {
    window.print = () => {
      const root = document.documentElement
      root.dataset.printCalls = String(Number(root.dataset.printCalls ?? '0') + 1)
    }
  })
}

/** The toolbar both printouts share: the way back to the event and the print button. */
class PrintoutPage {
  /** The printout, shown while it is the current page. */
  readonly root: Locator

  /** The link back to the admin page of the event. */
  readonly back: Locator

  /** The button that opens the print dialog. */
  readonly print: Locator

  constructor(
    protected readonly page: Page,
    route: 'admin-event-badges' | 'admin-event-roster',
  ) {
    this.root = page.getByTestId(`page-${route}`)
    this.back = this.root.getByTestId('print-toolbar-back')
    this.print = this.root.getByTestId('print-toolbar-print')
  }

  /** Waits until the page asked for the print dialog `count` times, once `stubPrint` ran. */
  async expectPrintCalls(count: number): Promise<void> {
    await expect(this.page.locator('html')).toHaveAttribute('data-print-calls', String(count))
  }
}

/** The badges printout, at `/admin/events/{id}/badges`: one label per confirmed attendee. */
export class BadgesPage extends PrintoutPage {
  /** The badges printed, one per person. */
  readonly badges: Locator

  /** The note the page shows while it loads, when it fails or when nobody is confirmed. */
  readonly empty: Locator

  constructor(page: Page) {
    super(page, 'admin-event-badges')
    this.badges = this.root.getByTestId('event-badge')
    this.empty = this.root.getByTestId('event-badges-state')
  }

  /** The badge of the person whose full name is `name`. */
  badge(name: string): Locator {
    return this.badges.filter({
      has: this.page.getByTestId('event-badge-name').filter({ hasText: name }),
    })
  }
}

/**
 * The roster printout, at `/admin/events/{id}/roster`: a table per activity with confirmed people.
 */
export class RosterPage extends PrintoutPage {
  /** The note the page shows while it loads, when it fails or when nobody is confirmed. */
  readonly empty: Locator

  constructor(page: Page) {
    super(page, 'admin-event-roster')
    this.empty = this.root.getByTestId('event-roster-state')
  }

  /**
   * The heading that opens the table of the activity `title`. The page also lays out a hidden copy
   * of every table to measure it, which this leaves out.
   */
  activity(title: string): Locator {
    return this.root
      .getByTestId('roster-chunk-title')
      .filter({ hasText: title })
      .filter({ visible: true })
  }

  /** The row of the person whose full name is `name`, leaving out the hidden measuring copy. */
  person(name: string): Locator {
    return this.root
      .getByTestId('roster-chunk-person')
      .filter({ has: this.page.getByTestId('roster-chunk-name').filter({ hasText: name }) })
      .filter({ visible: true })
  }
}
