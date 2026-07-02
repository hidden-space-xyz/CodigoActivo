import { computed, ref, type Ref } from 'vue'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'
import type {
  DataTableFilterMetaData,
  DataTablePageEvent,
  DataTableSortEvent,
} from 'primevue/datatable'

import {
  buildColumnFilter,
  combineFilters,
  fetchODataList,
  type ODataFieldType,
  type ODataPage,
  type ODataQuery,
} from '@/shared/api'

export interface ODataColumn {
  readonly field?: string
  readonly type: ODataFieldType
  readonly matchMode?: string
}

interface UseODataTableOptions {
  readonly resource: string
  readonly queryKey: readonly unknown[]
  readonly columns?: Record<string, ODataColumn> | undefined
  readonly baseFilter?: Ref<string | undefined> | (() => string | undefined) | undefined
  readonly defaultSort?: { readonly field: string; readonly order?: 1 | -1 } | undefined
  readonly rows?: number | undefined
}

const DEFAULT_MATCH_MODE: Record<ODataFieldType, string> = {
  text: 'contains',
  numeric: 'equals',
  boolean: 'equals',
  date: 'dateIs',
  datetime: 'dateIs',
  guid: 'equals',
}

function initialFilters(
  columns: Record<string, ODataColumn>,
): Record<string, DataTableFilterMetaData> {
  const filters: Record<string, DataTableFilterMetaData> = {}
  for (const [key, column] of Object.entries(columns)) {
    filters[key] = { value: null, matchMode: column.matchMode ?? DEFAULT_MATCH_MODE[column.type] }
  }
  return filters
}

export function useODataTable<T>(options: UseODataTableOptions) {
  const columns = options.columns ?? {}
  const first = ref(0)
  const rows = ref(options.rows ?? 25)
  const sortField = ref<string | undefined>(options.defaultSort?.field)
  const sortOrder = ref<number>(options.defaultSort?.order ?? 1)
  const filters = ref<Record<string, DataTableFilterMetaData>>(initialFilters(columns))

  const resolvedBaseFilter = computed(() =>
    typeof options.baseFilter === 'function' ? options.baseFilter() : options.baseFilter?.value,
  )

  const query = computed<ODataQuery>(() => {
    const clauses: Array<string | undefined> = [resolvedBaseFilter.value]

    for (const [key, column] of Object.entries(columns)) {
      const meta = filters.value[key] as DataTableFilterMetaData | undefined
      if (!meta) continue
      clauses.push(
        buildColumnFilter(
          column.field ?? key,
          column.type,
          meta.matchMode ?? undefined,
          meta.value,
        ),
      )
    }

    const orderBy = sortField.value
      ? `${sortField.value} ${sortOrder.value === -1 ? 'desc' : 'asc'}`
      : undefined
    const filter = combineFilters(...clauses)

    return {
      top: rows.value,
      skip: first.value,
      count: true,
      ...(orderBy ? { orderBy } : {}),
      ...(filter ? { filter } : {}),
    }
  })

  const tableQuery = useQuery({
    queryKey: computed(() => [...options.queryKey, query.value]),
    queryFn: () => fetchODataList<T>(options.resource, query.value),
    placeholderData: keepPreviousData,
  })

  const page = computed<ODataPage<T>>(() => tableQuery.data.value ?? { items: [], total: 0 })

  function onPage(event: DataTablePageEvent): void {
    first.value = event.first
    rows.value = event.rows
  }

  function onSort(event: DataTableSortEvent): void {
    sortField.value = typeof event.sortField === 'string' ? event.sortField : undefined
    sortOrder.value = event.sortOrder ?? 1
    first.value = 0
  }

  function onFilter(): void {
    first.value = 0
  }

  function columnFilter(key: string): DataTableFilterMetaData {
    const existing = filters.value[key]
    if (existing) return existing
    const meta: DataTableFilterMetaData = { value: null, matchMode: undefined }
    filters.value[key] = meta
    return meta
  }

  return {
    items: computed(() => page.value.items),
    total: computed(() => page.value.total),
    loading: tableQuery.isFetching,
    isError: tableQuery.isError,
    first,
    rows,
    sortField,
    sortOrder,
    filters,
    columnFilter,
    onPage,
    onSort,
    onFilter,
    refetch: tableQuery.refetch,
  }
}
