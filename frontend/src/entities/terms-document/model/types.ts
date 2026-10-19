/**
 * Whether a terms document holds the conditions of taking part, which an event may require and the
 * account holder accepts for each person they sign up, or a consent, such as the use of images,
 * which is never required and which a minor decides from the age of 14.
 */
export type TermsDocumentKind = 'Conditions' | 'Consent'

/**
 * Terms participants accept or decline when signing up for an event; `description` is a rich-text
 * document and `requiresSignature` asks for a signature drawn on screen when accepting it.
 */
export interface TermsDocument {
  readonly id: string
  readonly name: string
  readonly description: string
  readonly requiresSignature: boolean
  readonly kind: TermsDocumentKind
}

/** Values a terms document is created or updated with. */
export interface TermsDocumentInput {
  readonly name: string
  readonly description: string
  readonly requiresSignature: boolean
  readonly kind: TermsDocumentKind
}

/** Filters, sort and page of the admin terms document list. */
export interface TermsDocumentListParams {
  readonly name?: string
  readonly page?: number
  readonly pageSize?: number
  readonly sort?: string
}
