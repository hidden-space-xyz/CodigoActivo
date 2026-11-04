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
  /** The link back to the admin page of the event. */
  readonly back: Locator

  /** The button that opens the print dialog. */
  readonly print: Locator

  constructor(protected readonly page: Page) {
    this.back = page.getByRole('link', { name: 'Volver al evento' })
    this.print = page.getByRole('button', { name: 'Imprimir', exact: true })
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

  /** The note shown when nobody is confirmed. */
  readonly empty: Locator

  constructor(page: Page) {
    super(page)
    this.badges = page.locator('article.badge')
    this.empty = page.getByText(
      'Este evento no tiene asignaciones confirmadas, así que no hay etiquetas que imprimir.',
    )
  }

  /** The badge of the person whose full name is exactly `name`. */
  badge(name: string): Locator {
    return this.badges.filter({
      has: this.page.getByRole('heading', { name, exact: true }),
    })
  }
}

/** The roster printout, at `/admin/events/{id}/roster`: a table per activity with confirmed people. */
export class RosterPage extends PrintoutPage {
  /** The note shown when nobody is confirmed. */
  readonly empty: Locator

  constructor(page: Page) {
    super(page)
    this.empty = page.getByText(
      'Este evento no tiene asignaciones confirmadas, así que no hay listados que imprimir.',
    )
  }

  /** The heading that opens the table of the activity `title`. */
  activity(title: string): Locator {
    return this.page.getByRole('heading', { level: 2, name: title, exact: true })
  }

  /** The row of the person whose full name is exactly `name`. */
  person(name: string): Locator {
    return this.page
      .getByRole('row')
      .filter({ has: this.page.getByRole('cell', { name, exact: true }) })
  }
}
