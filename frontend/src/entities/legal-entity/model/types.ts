/** Contact phone as the association writes it and in the dialable form `tel:` links take. */
export interface ContactPhone {
  readonly text: string
  /** Digits and leading `+` only, for `tel:` links. */
  readonly dialable: string
}

/**
 * Legal identity and contact details of the association that runs the site, the data controller
 * named by the privacy policy. Each deployment configures them through the `LEGAL_*` settings, so
 * personal details stay out of the repository; a value is empty only outside production.
 */
export interface LegalEntity {
  readonly name: string
  /** Tax identification number (NIF). */
  readonly taxId: string
  readonly address: string
  /** Register of associations and the entry number in it. */
  readonly registry: string
  /** Contact mailbox, also the address promotional email gives to unsubscribe. */
  readonly email: string
  /** `null` when the association publishes no phone. */
  readonly phone: ContactPhone | null
}
