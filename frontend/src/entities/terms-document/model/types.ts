/**
 * Terms participants accept when signing up for an event; `description` is a rich-text document and
 * `requiresSignature` asks for a signature drawn on screen when accepting it.
 */
export interface TermsDocument {
  readonly id: string
  readonly name: string
  readonly description: string
  readonly requiresSignature: boolean
}

/** Values a terms document is created or updated with. */
export interface TermsDocumentInput {
  readonly name: string
  readonly description: string
  readonly requiresSignature: boolean
}

/** Filters, sort and page of the admin terms document list. */
export interface TermsDocumentListParams {
  readonly name?: string
  readonly page?: number
  readonly pageSize?: number
  readonly sort?: string
}
