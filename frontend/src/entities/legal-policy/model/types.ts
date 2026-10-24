/** Which policy of the site a legal policy is: the privacy policy or the cookie policy. */
export type LegalPolicyKind = 'Privacy' | 'Cookies'

/**
 * One version of a legal policy, the one in force or one a later version replaced; `content` is a
 * rich-text document and `version` grows with every change.
 */
export interface LegalPolicy {
  readonly kind: LegalPolicyKind
  readonly version: number
  readonly content: string
  readonly publishedAt: string
}

/** When a version of a legal policy was published, without its content. */
export interface LegalPolicyVersion {
  readonly version: number
  readonly publishedAt: string
}
