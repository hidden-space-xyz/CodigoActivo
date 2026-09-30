import {
  deleteApiEventsTermsDocumentTermsDocumentId,
  getApiEventsTermsDocument,
  postApiEventsTermsDocument,
  putApiEventsTermsDocumentTermsDocumentId,
} from '@/shared/api/generated/endpoints/events/events'
import { toPage } from '@/shared/api'
import type { ServerTablePage } from '@/shared/lib/paging'

import type { TermsDocument, TermsDocumentInput, TermsDocumentListParams } from '../model/types'
import { toTermsDocument, toTermsDocumentRequest } from './mapper'

/** Loads up to 100 terms documents for the event form selector. */
export async function getTermsDocumentsRequest(): Promise<readonly TermsDocument[]> {
  const response = await getApiEventsTermsDocument({ pageSize: 100 })
  return toPage(response).items.map(toTermsDocument)
}

/** Fetches one page of the admin terms document list. */
export async function getTermsDocumentListPageRequest(
  params: TermsDocumentListParams,
): Promise<ServerTablePage<TermsDocument>> {
  const { items, total } = toPage(await getApiEventsTermsDocument(params))
  return { items: items.map(toTermsDocument), total }
}

/** Creates a terms document. */
export async function createTermsDocumentRequest(input: TermsDocumentInput): Promise<void> {
  await postApiEventsTermsDocument(toTermsDocumentRequest(input))
}

/** Renames a terms document or rewrites its content. */
export async function updateTermsDocumentRequest(
  id: string,
  input: TermsDocumentInput,
): Promise<void> {
  await putApiEventsTermsDocumentTermsDocumentId(id, toTermsDocumentRequest(input))
}

/** Deletes a terms document; the API refuses it once an event uses it or someone accepted it. */
export async function deleteTermsDocumentRequest(id: string): Promise<void> {
  await deleteApiEventsTermsDocumentTermsDocumentId(id)
}
