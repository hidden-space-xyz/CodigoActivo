import { computed, ref, watch } from 'vue'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'
import type {
  DataTableFilterMetaData,
  DataTablePageEvent,
  DataTableSortEvent,
} from 'primevue/datatable'

export type ServerTableFieldType = 'text' | 'number' | 'dateRange'

export interface ServerTableColumn {
  readonly param?: string
  readonly type?: ServerTableFieldType
  readonly fromParam?: string
  readonly toParam?: string
}

function toDateParam(value: unknown): string | undefined {
  if (!(value instanceof Date) || Number.isNaN(value.getTime())) return undefined
  const month = String(value.getMonth() + 1).padStart(2, '0')
  const day = String(value.getDate()).padStart(2, '0')
  return `${value.getFullYear()}-${month}-${day}`
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
  readonly extraParams?: (() => Record<string, unknown>) | undefined
  readonly enabled?: (() => boolean) | undefined
}

function initialFilters(
  columns: Record<string, ServerTableColumn>,
): Record<string, DataTableFilterMetaData> {
  const filters: Record<string, DataTableFilterMetaData> = {}
  for (const key of Object.keys(columns)) filters[key] = { value: null, matchMode: undefined }
  return filters
}

export function useServerTable<T, TParams = Record<string, unknown>>(
  options: UseServerTableOptions<T, TParams>,
) {
  const columns = options.columns ?? {}
  const first = ref(0)
  const rows = ref(options.rows ?? 25)
  const sortField = ref<string | undefined>(options.defaultSort?.field)
  const sortOrder = ref<number>(options.defaultSort?.order ?? 1)
  const filters = ref<Record<string, DataTableFilterMetaData>>(initialFilters(columns))
  const extra = computed<Record<string, unknown>>(() => options.extraParams?.() ?? {})

  watch(extra, () => {
    first.value = 0
  })

  const params = computed<Record<string, unknown>>(() => {
    const result: Record<string, unknown> = {
      page: Math.floor(first.value / rows.value) + 1,
      pageSize: rows.value,
      ...extra.value,
    }

    if (sortField.value) result.sort = `${sortOrder.value === -1 ? '-' : ''}${sortField.value}`

    for (const [key, column] of Object.entries(columns)) {
      const value = filters.value[key]?.value
      if (value === null || value === undefined || value === '') continue
      if (column.type === 'dateRange') {
        const range: unknown[] = Array.isArray(value) ? value : []
        const from = toDateParam(range[0])
        const to = toDateParam(range[1])
        if (from) result[column.fromParam ?? `${key}From`] = from
        if (to) result[column.toParam ?? `${key}To`] = to
      } else if (column.type === 'number') {
        const parsed = Number(value)
        if (!Number.isFinite(parsed)) continue
        result[column.param ?? key] = parsed
      } else {
        result[column.param ?? key] = value
      }
    }

    return result
  })

  const tableQuery = useQuery({
    queryKey: computed(() => [...options.queryKey, params.value]),
    queryFn: () => options.fetchPage(params.value as unknown as TParams),
    placeholderData: keepPreviousData,
    enabled: computed(() => options.enabled?.() ?? true),
  })

  const page = computed<ServerTablePage<T>>(() => tableQuery.data.value ?? { items: [], total: 0 })

  watch(page, (current) => {
    if (current.items.length === 0 && current.total > 0 && first.value > 0) {
      first.value = Math.floor((current.total - 1) / rows.value) * rows.value
    }
  })

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
