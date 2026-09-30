import { queryOptions } from '@tanstack/vue-query'

import type { ServerTableSource } from '@/shared/lib/paging'

import type { TermsDocument, TermsDocumentListParams } from '../model/types'
import { getTermsDocumentListPageRequest, getTermsDocumentsRequest } from './requests'

/** Query keys of terms documents; `all` covers the options and the admin list. */
export const termsDocumentKeys = {
  all: ['terms-documents'] as const,
  options: () => [...termsDocumentKeys.all, 'options'] as const,
  list: () => [...termsDocumentKeys.all, 'list'] as const,
}

/** Query options of terms documents. */
export const termsDocumentQueries = {
  /** Up to 100 terms documents for the event form selector. */
  options: () =>
    queryOptions({
      queryKey: termsDocumentKeys.options(),
      queryFn: () => getTermsDocumentsRequest(),
    }),
}

/** Admin terms document list, paged, filtered and sorted by the API. */
export const termsDocumentList: ServerTableSource<TermsDocument, TermsDocumentListParams> = {
  queryKey: termsDocumentKeys.list(),
  fetchPage: (params) => getTermsDocumentListPageRequest(params),
}
