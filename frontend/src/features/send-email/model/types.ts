/**
 * Who a manual email would reach, selected exactly like the send endpoints do: only users with an
 * address, each address once.
 */
export interface EmailAudience {
  readonly recipients: number
  /** Recipients that have not agreed to receive promotional content. */
  readonly withoutConsent: number
}
