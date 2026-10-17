import { queryOptions } from '@tanstack/vue-query'

import type { ServerTableSource } from '@/shared/lib/paging'

import type { DiagnosticEntry, DiagnosticListParams } from '../model/types'
import { getDiagnosticDetailRequest, getDiagnosticsPageRequest } from './requests'

/** Query keys of the diagnostic log. */
export const diagnosticKeys = {
  all: ['logs', 'diagnostics'] as const,
  list: () => [...diagnosticKeys.all, 'list'] as const,
  detail: (id: string) => [...diagnosticKeys.all, 'detail', id] as const,
}

/** Query options of the diagnostic log. */
export const diagnosticQueries = {
  /** One entry with its exception and template values. */
  detail: (id: string) =>
    queryOptions({
      queryKey: diagnosticKeys.detail(id),
      queryFn: () => getDiagnosticDetailRequest(id),
    }),
}

/** Diagnostic entries, paged, filtered and sorted by the API. */
export const diagnosticList: ServerTableSource<DiagnosticEntry, DiagnosticListParams> = {
  queryKey: diagnosticKeys.list(),
  fetchPage: (params) => getDiagnosticsPageRequest(params),
}
