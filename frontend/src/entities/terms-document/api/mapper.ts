import type {
  CreateTermsDocumentRequest,
  TermsDocumentResponse,
} from '@/shared/api/generated/models'

import type { TermsDocument, TermsDocumentInput } from '../model/types'

/** Maps a terms document. */
export function toTermsDocument(termsDocument: TermsDocumentResponse): TermsDocument {
  return { id: termsDocument.id, name: termsDocument.name, description: termsDocument.description }
}

/** Builds the body that creates or updates a terms document; both endpoints take the same fields. */
export function toTermsDocumentRequest(input: TermsDocumentInput): CreateTermsDocumentRequest {
  return { name: input.name, description: input.description }
}
