import type { Locator } from '@playwright/test'

import type { AccountPage } from './account-page'

/** The section «Consentimientos» of the profile tab. */
export class ConsentsSection {
  /** The section itself. */
  readonly root: Locator

  /** The message shown while nobody in the household has a consent decided. */
  readonly empty: Locator

  constructor(account: AccountPage) {
    this.root = account.section('Consentimientos')
    this.empty = this.root.getByText(
      'No hay consentimientos decididos para ti ni para los menores a tu cargo.',
    )
  }

  /** The consent about the document `document` decided for the person `person`. */
  consent(document: string, person: string): Locator {
    return this.root.locator('li.acc-consent').filter({ hasText: `${document} · ${person}` })
  }
}
