import { computed, ref, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'

import { useServerTable } from '@/shared/lib/paging'
import type { TableFilterChip } from '@/shared/ui/data-table'

import { diagnosticList, diagnosticQueries } from '../api/queries'
import type { DiagnosticEntry } from './types'

const TRACE_PATTERN = /^[0-9a-f]{32}$/iu

/** The trace a `?traceId=` query names, lowercased, or empty when it names none. */
function traceOf(value: unknown): string {
  return typeof value === 'string' && TRACE_PATTERN.test(value) ? value.toLowerCase() : ''
}

/**
 * Diagnostic page: the entries the API logged, newest first, with a message search, level,
 * source and date filters, a trace filter the page owns (taken from and kept in `?traceId=`, so the
 * audit page and an error reference can link here) and the dialog that loads one entry with its
 * exception. Call it in `setup`.
 */
export function useDiagnostics() {
  const { t } = useI18n()
  const route = useRoute()
  const router = useRouter()
  const traceId = ref(traceOf(route.query.traceId))

  watch(
    () => route.query.traceId,
    (value) => {
      traceId.value = traceOf(value)
    },
  )

  const table = useServerTable({
    ...diagnosticList,
    defaultSort: { field: 'occurredAt', order: -1 },
    columns: {
      message: { type: 'text', param: 'search' },
      level: { param: 'level' },
      source: { type: 'text', param: 'category' },
      occurred: { type: 'dateRange', fromParam: 'occurredFrom', toParam: 'occurredTo' },
    },
    extraParams: () => (traceId.value ? { traceId: traceId.value } : {}),
  })

  /** Narrows the list to the entries of one trace, or lifts that filter with an empty `trace`. */
  function filterByTrace(trace: string): void {
    traceId.value = traceOf(trace)
    void router.replace({ query: traceId.value ? { traceId: traceId.value } : {} })
  }

  /** Opens the audit page on the commands of the same trace. */
  function openAudit(trace: string): void {
    void router.push({ name: 'admin-audit', query: { traceId: trace } })
  }

  const extraFilters = computed<TableFilterChip[]>(() =>
    traceId.value
      ? [
          {
            key: 'traceId',
            label: t('pages.admin.diagnostics.filters.trace', { trace: traceId.value }),
            remove: () => {
              filterByTrace('')
            },
          },
        ]
      : [],
  )

  const selectedId = ref<string | null>(null)
  const detailVisible = ref(false)
  const detailQuery = useQuery(() => ({
    ...diagnosticQueries.detail(selectedId.value ?? ''),
    enabled: selectedId.value !== null,
  }))

  /** Opens the dialog of `entry`, which loads its exception and template values. */
  function openDetail(entry: DiagnosticEntry): void {
    selectedId.value = entry.id
    detailVisible.value = true
  }

  return {
    table,
    extraFilters,
    filterByTrace,
    openAudit,
    detail: {
      visible: detailVisible,
      entry: detailQuery.data,
      loading: detailQuery.isLoading,
      failed: detailQuery.isError,
    },
    openDetail,
  }
}
