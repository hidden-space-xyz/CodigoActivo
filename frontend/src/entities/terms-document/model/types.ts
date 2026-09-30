/** Terms participants accept when signing up for an event; `description` is a rich-text document. */
export interface TermsDocument {
  readonly id: string
  readonly name: string
  readonly description: string
}

/** Values a terms document is created or updated with. */
export interface TermsDocumentInput {
  readonly name: string
  readonly description: string
}

/** Filters, sort and page of the admin terms document list. */
export interface TermsDocumentListParams {
  readonly name?: string
  readonly page?: number
  readonly pageSize?: number
  readonly sort?: string
}
