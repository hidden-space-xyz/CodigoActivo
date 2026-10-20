import { getApiLogsAudit } from '@/shared/api/generated/endpoints/logs/logs'
import { toPage } from '@/shared/api'
import type { ServerTablePage } from '@/shared/lib/paging'

import type { AuditEntry, AuditListParams } from '../model/types'
import { toAuditEntry } from './mapper'

/** Fetches one page of the audit log. */
export async function getAuditPageRequest(
  params: AuditListParams,
): Promise<ServerTablePage<AuditEntry>> {
  return toPage(await getApiLogsAudit(params), toAuditEntry)
}
