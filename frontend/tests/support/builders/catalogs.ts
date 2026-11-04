import type {
  EventCategoryTypeResponse,
  TermsDocumentResponse,
} from '@/shared/api/generated/models'

import { richText } from './common'

/** Event category as the categories catalog returns it. */
export function buildEventCategoryType(
  overrides: Partial<EventCategoryTypeResponse> = {},
): EventCategoryTypeResponse {
  return { id: 'category-1', name: 'Workshop', color: '#FF0000', ...overrides }
}

/** Terms document as the terms catalog returns it. */
export function buildTermsDocument(
  overrides: Partial<TermsDocumentResponse> = {},
): TermsDocumentResponse {
  return {
    id: 'terms-1',
    name: 'Privacy',
    description: richText('Terms body'),
    requiresSignature: false,
    kind: 'Conditions',
    version: 1,
    ...overrides,
  }
}
