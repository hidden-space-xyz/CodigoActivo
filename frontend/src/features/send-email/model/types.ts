/**
 * Who a manual email would reach, selected exactly like the send endpoints do: only users with an
 * address, each address once.
 */
export interface EmailAudience {
  readonly recipients: number
  /** Recipients that have not agreed to receive promotional content. */
  readonly withoutConsent: number
}

/** Outcome of sending an email: how many copies were queued and how many recipients skipped. */
export interface SendEmailResult {
  readonly queued: number
  readonly skipped: number
}

/** Email composed in the dialog; subject and body are trimmed before sending. */
export interface SendEmailPayload {
  readonly subject: string
  readonly body: string
  readonly attachments: readonly File[]
}

/** Filters selecting the recipients, exactly as the list they come from sends them to the API. */
export type RecipientFilters = Readonly<Record<string, unknown>>
