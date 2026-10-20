import {
  deleteApiEventsTermsDocumentTermsDocumentId,
  getApiEventsTermsDocument,
  postApiEventsTermsDocument,
  putApiEventsTermsDocumentTermsDocumentId,
} from '@/shared/api/generated/endpoints/events/events'
import { toPage } from '@/shared/api'
import { MAX_PAGE_SIZE } from '@/shared/config'
import type { ServerTablePage } from '@/shared/lib/paging'

import type { TermsDocument, TermsDocumentInput, TermsDocumentListParams } from '../model/types'
import { toTermsDocument, toTermsDocumentRequest } from './mapper'

/** Loads up to 100 terms documents for the event form selector. */
export async function getTermsDocumentsRequest(): Promise<readonly TermsDocument[]> {
  const response = await getApiEventsTermsDocument({ pageSize: MAX_PAGE_SIZE })
  return toPage(response, toTermsDocument).items
}

/** Fetches one page of the admin terms document list. */
export async function getTermsDocumentListPageRequest(
  params: TermsDocumentListParams,
): Promise<ServerTablePage<TermsDocument>> {
  return toPage(await getApiEventsTermsDocument(params), toTermsDocument)
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
