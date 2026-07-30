import { computed, ref, watch } from 'vue'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'
import type {
  DataTableFilterMetaData,
  DataTableProps,
  DataTableSortEvent,
} from 'primevue/datatable'

import { toDateOnly } from './format'

export type ServerTableFieldType = 'text' | 'number' | 'dateRange'

const ROWS_PER_PAGE_OPTIONS = [25, 50, 100]

export interface ServerTableColumn {
  readonly param?: string
  readonly type?: ServerTableFieldType
  readonly fromParam?: string
  readonly toParam?: string
}

function toDateParam(value: unknown): string | undefined {
  if (!(value instanceof Date) || Number.isNaN(value.getTime())) return undefined
  return toDateOnly(value)
}

export interface ServerTablePage<T> {
  readonly items: T[]
  readonly total: number
}

const FETCH_ALL_PAGE_SIZE = 100
const FETCH_ALL_PAGE_LIMIT = 200

export async function fetchAllPages<T>(
  fetchPage: (params: Record<string, unknown>) => Promise<ServerTablePage<T>>,
  params: Record<string, unknown>,
): Promise<T[]> {
  const collected: T[] = []

  for (let page = 1; page <= FETCH_ALL_PAGE_LIMIT; page += 1) {
    const { items, total } = await fetchPage({
      ...params,
      page,
      pageSize: FETCH_ALL_PAGE_SIZE,
    })
    collected.push(...items)
    if (items.length === 0 || collected.length >= total) break
  }

  return collected
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

  const filterParams = computed<Record<string, unknown>>(() => {
    const result: Record<string, unknown> = { ...extra.value }

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

  const sortParam = computed(() =>
    sortField.value ? `${sortOrder.value === -1 ? '-' : ''}${sortField.value}` : undefined,
  )

  const params = computed<Record<string, unknown>>(() => {
    const result: Record<string, unknown> = {
      page: Math.floor(first.value / rows.value) + 1,
      pageSize: rows.value,
      ...filterParams.value,
    }

    if (sortParam.value) result.sort = sortParam.value

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

  const dataTableProps = computed(
    () =>
      ({
        lazy: true,
        value: page.value.items,
        totalRecords: page.value.total,
        loading: tableQuery.isFetching.value,
        dataKey: 'id',
        stripedRows: true,
        paginator: true,
        rows: rows.value,
        first: first.value,
        rowsPerPageOptions: ROWS_PER_PAGE_OPTIONS,
        sortField: sortField.value,
        sortOrder: sortOrder.value,
        removableSort: true,
      }) satisfies DataTableProps,
  )

  function onPage(event: { first: number; rows: number }): void {
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

  function clearFilters(): void {
    for (const meta of Object.values(filters.value)) meta.value = null
    onFilter()
  }

  function columnFilter(key: string): DataTableFilterMetaData {
    const existing = filters.value[key]
    if (existing) return existing
    const meta: DataTableFilterMetaData = { value: null, matchMode: undefined }
    filters.value[key] = meta
    return meta
  }

  return {
    dataTableProps,
    items: computed(() => page.value.items),
    total: computed(() => page.value.total),
    loading: tableQuery.isFetching,
    isError: tableQuery.isError,
    first,
    rows,
    sortField,
    sortOrder,
    filterParams,
    sortParam,
    columnFilter,
    clearFilters,
    onPage,
    onSort,
    onFilter,
  }
}
