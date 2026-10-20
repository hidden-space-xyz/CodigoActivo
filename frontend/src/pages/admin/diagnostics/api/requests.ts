import {
  getApiLogsDiagnostics,
  getApiLogsDiagnosticsId,
} from '@/shared/api/generated/endpoints/logs/logs'
import { toPage } from '@/shared/api'
import type { ServerTablePage } from '@/shared/lib/paging'

import type { DiagnosticDetail, DiagnosticEntry, DiagnosticListParams } from '../model/types'
import { toDiagnosticDetail, toDiagnosticEntry } from './mapper'

/** Fetches one page of the diagnostic log. */
export async function getDiagnosticsPageRequest(
  params: DiagnosticListParams,
): Promise<ServerTablePage<DiagnosticEntry>> {
  return toPage(await getApiLogsDiagnostics(params), toDiagnosticEntry)
}

/** Loads one diagnostic entry with its exception. */
export async function getDiagnosticDetailRequest(id: string): Promise<DiagnosticDetail> {
  const { data } = await getApiLogsDiagnosticsId(id)
  return toDiagnosticDetail(data)
}
