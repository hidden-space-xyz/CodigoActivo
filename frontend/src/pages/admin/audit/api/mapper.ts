import type { AuditLogEntryResponse } from '@/shared/api/generated/models'

import type { AuditEntry } from '../model/types'

/** Maps an audit list row; missing values become empty strings. */
export function toAuditEntry(dto: AuditLogEntryResponse): AuditEntry {
  return {
    id: dto.id,
    occurredAt: dto.occurredAt,
    actorType: dto.actorType,
    actorId: dto.actorId ?? '',
    actorName: dto.actorName ?? '',
    actorEmail: dto.actorEmail ?? '',
    clientIp: dto.clientIp ?? '',
    action: dto.action,
    entity:
      dto.entityType && dto.entityId
        ? { type: dto.entityType, id: dto.entityId, name: dto.entityName ?? '' }
        : null,
    related: dto.related.map((related) => ({ type: related.type, id: related.id })),
    outcome: dto.outcome,
    errorCode: dto.errorCode ?? '',
    traceId: dto.traceId ?? '',
  }
}
