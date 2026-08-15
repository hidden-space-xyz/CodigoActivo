import type {
  GetApiEventsTermsDocumentParams,
  TermsDocumentResponse,
} from '@/shared/api/generated/models'
import { useServerTable } from '@/shared/lib'
import {
  catalogQueryKeys,
  createTermsDocumentRequest,
  deleteTermsDocumentRequest,
  getTermsDocumentsPageRequest,
  updateTermsDocumentRequest,
} from '@/entities/catalog'

import { useCatalog } from './useCatalog'

export interface TermsDocumentInput {
  name: string
  description: string
}

export function useTermsDocuments() {
  const table = useServerTable<TermsDocumentResponse, GetApiEventsTermsDocumentParams>({
    queryKey: catalogQueryKeys.termsDocumentsTable(),
    fetchPage: (params) => getTermsDocumentsPageRequest(params),
    defaultSort: { field: 'name', order: 1 },
    columns: {
      name: { type: 'text' },
    },
  })

  const { create, update, remove } = useCatalog<TermsDocumentInput>({
    queryKey: catalogQueryKeys.termsDocuments(),
    create: (body) => createTermsDocumentRequest(body),
    update: (id, body) => updateTermsDocumentRequest(id, body),
    remove: (id) => deleteTermsDocumentRequest(id),
  })

  return { table, create, update, remove }
}
