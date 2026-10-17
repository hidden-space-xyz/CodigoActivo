import type {
  DiagnosticLogEntryDetailResponse,
  DiagnosticLogEntryResponse,
} from '@/shared/api/generated/models'

import type { DiagnosticDetail, DiagnosticEntry } from '../model/types'

/** Indents a JSON object for reading; text that is not JSON is kept as it came. */
function indentJson(json: string | null | undefined): string {
  if (!json) return ''
  try {
    return JSON.stringify(JSON.parse(json), null, 2)
  } catch {
    return json
  }
}

/** Maps a diagnostic list row. */
export function toDiagnosticEntry(dto: DiagnosticLogEntryResponse): DiagnosticEntry {
  return {
    id: dto.id,
    occurredAt: dto.occurredAt,
    level: dto.level,
    category: dto.category,
    eventName: dto.eventName ?? '',
    message: dto.message,
    hasException: dto.hasException,
    traceId: dto.traceId ?? '',
  }
}

/** Maps a diagnostic entry with its exception and template values. */
export function toDiagnosticDetail(dto: DiagnosticLogEntryDetailResponse): DiagnosticDetail {
  return {
    id: dto.id,
    occurredAt: dto.occurredAt,
    level: dto.level,
    category: dto.category,
    eventName: dto.eventName ?? '',
    message: dto.message,
    exception: dto.exception ?? '',
    properties: indentJson(dto.properties),
    traceId: dto.traceId ?? '',
  }
}
