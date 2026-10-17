import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'

import { useServerTable } from '@/shared/lib/paging'
import type { TableFilterChip } from '@/shared/ui/data-table'

import { auditList } from '../api/queries'
import { actorLabel, entityLabel, entityTypeLabelKey, shortId } from '../lib/labels'
import type { AuditEntityRef, AuditEntry, AuditMainEntity } from './types'

const TRACE_PATTERN = /^[0-9a-f]{32}$/iu

/** One page-owned filter: the value sent to the API and the text of its chip. */
interface PinnedFilter {
  readonly value: string
  readonly label: string
}

/** The trace a `?traceId=` query names, lowercased, or empty when it names none. */
function traceOf(value: unknown): string {
  return typeof value === 'string' && TRACE_PATTERN.test(value) ? value.toLowerCase() : ''
}

/**
 * Audit page: the commands the application ran, newest first, with a person search, date, actor
 * type, command, entity type and outcome filters, and the filters a click pins — an actor, an
 * entity (also among related ones), an address or a trace. The trace is taken from and kept in
 * `?traceId=`, so the diagnostic page can link here. Call it in `setup`.
 */
export function useAuditLog() {
  const { t } = useI18n()
  const route = useRoute()
  const router = useRouter()
  const traceId = ref(traceOf(route.query.traceId))
  const actor = ref<PinnedFilter | null>(null)
  const entity = ref<PinnedFilter | null>(null)
  const clientIp = ref('')

  watch(
    () => route.query.traceId,
    (value) => {
      traceId.value = traceOf(value)
    },
  )

  const table = useServerTable({
    ...auditList,
    defaultSort: { field: 'occurredAt', order: -1 },
    columns: {
      person: { type: 'text', param: 'search' },
      occurred: { type: 'dateRange', fromParam: 'occurredFrom', toParam: 'occurredTo' },
      actorType: { param: 'actorType' },
      action: { param: 'action' },
      entityType: { param: 'entityType' },
      outcome: { param: 'outcome' },
    },
    extraParams: () => ({
      ...(actor.value ? { actorId: actor.value.value } : {}),
      ...(entity.value ? { entityId: entity.value.value } : {}),
      ...(clientIp.value ? { clientIp: clientIp.value } : {}),
      ...(traceId.value ? { traceId: traceId.value } : {}),
    }),
  })

  /** Keeps only the commands the actor of `entry` ran. */
  function filterByActor(entry: AuditEntry): void {
    actor.value = entry.actorId ? { value: entry.actorId, label: actorLabel(entry, t) } : null
  }

  /** Keeps only the entries about `target`, as their main or a related entity. */
  function filterByEntity(target: AuditMainEntity | AuditEntityRef): void {
    entity.value = {
      value: target.id,
      label:
        'name' in target
          ? entityLabel(target, t)
          : `${t(entityTypeLabelKey(target.type))} ${shortId(target.id)}`,
    }
  }

  /** Keeps only the commands that came from `address`. */
  function filterByIp(address: string): void {
    clientIp.value = address
  }

  /** Keeps only the commands of one trace, or lifts that filter with an empty `trace`. */
  function filterByTrace(trace: string): void {
    traceId.value = traceOf(trace)
    void router.replace({ query: traceId.value ? { traceId: traceId.value } : {} })
  }

  /** Opens the diagnostic page on the events of the same trace. */
  function openDiagnostics(trace: string): void {
    void router.push({ name: 'admin-diagnostics', query: { traceId: trace } })
  }

  const extraFilters = computed<TableFilterChip[]>(() => [
    ...(actor.value
      ? [
          {
            key: 'actor',
            label: t('pages.admin.audit.filters.actor', { actor: actor.value.label }),
            remove: () => {
              actor.value = null
            },
          },
        ]
      : []),
    ...(entity.value
      ? [
          {
            key: 'entity',
            label: t('pages.admin.audit.filters.entity', { entity: entity.value.label }),
            remove: () => {
              entity.value = null
            },
          },
        ]
      : []),
    ...(clientIp.value
      ? [
          {
            key: 'clientIp',
            label: t('pages.admin.audit.filters.ip', { ip: clientIp.value }),
            remove: () => {
              clientIp.value = ''
            },
          },
        ]
      : []),
    ...(traceId.value
      ? [
          {
            key: 'traceId',
            label: t('pages.admin.audit.filters.trace', { trace: traceId.value }),
            remove: () => {
              filterByTrace('')
            },
          },
        ]
      : []),
  ])

  return {
    table,
    extraFilters,
    filterByActor,
    filterByEntity,
    filterByIp,
    filterByTrace,
    openDiagnostics,
  }
}
