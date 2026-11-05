import type { Locator } from '@playwright/test'

import type { AccountPage } from './account-page'

/** The section «Consentimientos» of the profile tab. */
export class ConsentsSection {
  /** The section itself. */
  readonly root: Locator

  /** The message shown while nobody in the household has a consent decided. */
  readonly empty: Locator

  constructor(account: AccountPage) {
    this.root = account.section('consents')
    this.empty = this.root.getByTestId('consents-empty')
  }

  /** The consent about the document `document` decided for the person `person`. */
  consent(document: string, person: string): Locator {
    return this.root.getByTestId('consents-item').filter({ hasText: `${document} · ${person}` })
  }
}
