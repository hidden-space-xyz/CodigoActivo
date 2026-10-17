import type { ServerTableSource } from '@/shared/lib/paging'

import type { AuditEntry, AuditListParams } from '../model/types'
import { getAuditPageRequest } from './requests'

/** Query keys of the audit log. */
export const auditKeys = {
  all: ['logs', 'audit'] as const,
  list: () => [...auditKeys.all, 'list'] as const,
}

/** Audit entries, paged, filtered and sorted by the API. */
export const auditList: ServerTableSource<AuditEntry, AuditListParams> = {
  queryKey: auditKeys.list(),
  fetchPage: (params) => getAuditPageRequest(params),
}
