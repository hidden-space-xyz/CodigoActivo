import type {
  EventCategoryTypeResponse,
  TermsDocumentResponse,
} from '@/shared/api/generated/models'

import { assignmentStatusTypes, modalityTypes, roleTypes } from '../builders/activities'
import { buildFileResponse } from '../builders/catalogs'
import { userTypes } from '../builders/users'
import { http, HttpResponse, paged, server } from '../server'

const categoryTypes: EventCategoryTypeResponse[] = [
  { id: 'cat-1', name: 'Tech', color: '#112233' },
  { id: 'cat-2', name: 'Art', color: '#445566' },
]

const termsDocuments: TermsDocumentResponse[] = [{ id: 'terms-1', name: 'Terms', description: '' }]

/** Registers the catalog endpoints used across the admin event screens. */
export function useCatalogHandlers(): void {
  server.use(
    http.get('/api/events/categoryType', () => HttpResponse.json(paged(categoryTypes))),
    http.get('/api/events/termsDocument', () => HttpResponse.json(paged(termsDocuments))),
    http.get('/api/activities/modality-types', () => HttpResponse.json(modalityTypes)),
    http.get('/api/activities/roleType', () => HttpResponse.json(roleTypes)),
    http.get('/api/activities/assignment-status-types', () =>
      HttpResponse.json(assignmentStatusTypes),
    ),
    http.get('/api/users/types', () => HttpResponse.json(userTypes)),
    http.get('/api/files/:fileId', () => HttpResponse.json(buildFileResponse())),
  )
}
