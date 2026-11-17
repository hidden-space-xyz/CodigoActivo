import type { Locator, Page } from '@playwright/test'

/** The tab «Consentimientos»: the consents decided for the household, which can be withdrawn. */
export class ConsentsTab {
  /** The content of the tab. */
  readonly root: Locator

  /** The message shown while nobody in the household has a consent decided. */
  readonly empty: Locator

  constructor(page: Page) {
    this.root = page.getByTestId('consents-section')
    this.empty = this.root.getByTestId('consents-empty')
  }

  /** The consent about the document `document` decided for the person `person`. */
  consent(document: string, person: string): Locator {
    return this.root.getByTestId('consents-item').filter({ hasText: `${document} · ${person}` })
  }
}
