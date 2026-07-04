import { computed, ref } from 'vue'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'
import type {
  DataTableFilterMetaData,
  DataTablePageEvent,
  DataTableSortEvent,
} from 'primevue/datatable'

export type ServerTableFieldType = 'text' | 'number'

/** A filterable column: maps a table column to a REST query parameter. */
export interface ServerTableColumn {
  /** Query parameter name to send (defaults to the column key). */
  readonly param?: string
  /** How to coerce the filter value (default 'text'). */
  readonly type?: ServerTableFieldType
}

export interface ServerTablePage<T> {
  readonly items: T[]
  readonly total: number
}

interface UseServerTableOptions<T, TParams> {
  readonly queryKey: readonly unknown[]
  readonly fetchPage: (params: TParams) => Promise<ServerTablePage<T>>
  readonly columns?: Record<string, ServerTableColumn> | undefined
  readonly defaultSort?: { readonly field: string; readonly order?: 1 | -1 } | undefined
  readonly rows?: number | undefined
}

function initialFilters(
  columns: Record<string, ServerTableColumn>,
): Record<string, DataTableFilterMetaData> {
  const filters: Record<string, DataTableFilterMetaData> = {}
  for (const key of Object.keys(columns)) filters[key] = { value: null, matchMode: undefined }
  return filters
}

/**
 * Server-side pagination/sorting/filtering for a PrimeVue lazy DataTable, backed by a REST list
 * endpoint. Builds a plain query-parameter object (`page`, `pageSize`, `sort`, plus one param per
 * filtered column) matching the backend's typed list queries.
 */
export function useServerTable<T, TParams = Record<string, unknown>>(
  options: UseServerTableOptions<T, TParams>,
) {
  const columns = options.columns ?? {}
  const first = ref(0)
  const rows = ref(options.rows ?? 25)
  const sortField = ref<string | undefined>(options.defaultSort?.field)
  const sortOrder = ref<number>(options.defaultSort?.order ?? 1)
  const filters = ref<Record<string, DataTableFilterMetaData>>(initialFilters(columns))

  const params = computed<Record<string, unknown>>(() => {
    const result: Record<string, unknown> = {
      page: Math.floor(first.value / rows.value) + 1,
      pageSize: rows.value,
    }

    if (sortField.value)
      result.sort = `${sortOrder.value === -1 ? '-' : ''}${sortField.value}`

    for (const [key, column] of Object.entries(columns)) {
      const value = filters.value[key]?.value
      if (value === null || value === undefined || value === '') continue
      result[column.param ?? key] = column.type === 'number' ? Number(value) : value
    }

    return result
  })

  const tableQuery = useQuery({
    queryKey: computed(() => [...options.queryKey, params.value]),
    queryFn: () => options.fetchPage(params.value as unknown as TParams),
    placeholderData: keepPreviousData,
  })

  const page = computed<ServerTablePage<T>>(
    () => tableQuery.data.value ?? { items: [], total: 0 },
  )

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
