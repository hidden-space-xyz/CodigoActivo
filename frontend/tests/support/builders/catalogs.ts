import type {
  EventCategoryTypeResponse,
  FileResponse,
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
  return { id: 'terms-1', name: 'Privacy', description: richText('Terms body'), ...overrides }
}

/** Stored file metadata as `GET /api/files/:fileId` returns it. */
export function buildFileResponse(overrides: Partial<FileResponse> = {}): FileResponse {
  return {
    id: 'file-1',
    name: 'cover',
    extension: '.png',
    uploadedAt: '2026-01-01T10:00:00Z',
    uploadedBy: 'admin-1',
    ...overrides,
  }
}
