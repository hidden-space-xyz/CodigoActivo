import { computed, ref, watch } from 'vue'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'

import { toDateOnly } from '@/shared/lib/date'
import { useMediaQuery } from '@/shared/lib/media-query'

type ServerTableFieldType = 'text' | 'number' | 'dateRange'
/** Sort direction names used by Element Plus tables. */
export type ServerTableSortOrder = 'ascending' | 'descending'

const ROWS_PER_PAGE_OPTIONS = [25, 50, 100]
const PAGINATION_LAYOUT = 'total, sizes, prev, pager, next'
const PAGINATION_LAYOUT_NARROW = 'prev, pager, next'
const NARROW_QUERY = '(max-width: 640px)'
const DATE_ONLY_PATTERN = /^\d{4}-\d{2}-\d{2}$/

/**
 * Maps a filterable column to query parameters. `param` defaults to the column key; `dateRange`
 * columns send `fromParam`/`toParam` (default `<key>From`/`<key>To`) as `YYYY-MM-DD`.
 */
interface ServerTableColumn<TParams = Record<string, unknown>> {
  readonly param?: Extract<keyof TParams, string>
  readonly type?: ServerTableFieldType
  readonly fromParam?: Extract<keyof TParams, string>
  readonly toParam?: Extract<keyof TParams, string>
}

type ServerTableFilterValue =
  string | number | boolean | Date | readonly (Date | string | null)[] | null | undefined

/**
 * Write-only view of a column filter for `v-model`. The `never` getter lets any input component
 * bind to it without casts.
 */
export interface ServerTableFilter {
  get value(): never
  set value(next: ServerTableFilterValue)
}

/** Payload of the Element Plus table `sort-change` event; a `null` order clears sorting. */
export interface ServerTableSortChange {
  readonly prop: string | null
  readonly order: ServerTableSortOrder | null
}

/** Initial sort passed to the Element Plus table `default-sort` prop. */
export interface ServerTableDefaultSort {
  readonly prop: string
  readonly order: ServerTableSortOrder
}

interface ServerTableFilterState {
  value: ServerTableFilterValue
}

function toDateParam(value: unknown): string | undefined {
  if (value instanceof Date) {
    return Number.isNaN(value.getTime()) ? undefined : toDateOnly(value)
  }

  if (typeof value === 'string' && value !== '') {
    if (DATE_ONLY_PATTERN.test(value)) return value
    const parsed = new Date(value)
    return Number.isNaN(parsed.getTime()) ? undefined : toDateOnly(parsed)
  }

  return undefined
}

/** One page of rows; `total` counts all rows matching the filters. */
export interface ServerTablePage<T> {
  readonly items: T[]
  readonly total: number
}

const FETCH_ALL_PAGE_SIZE = 100
const FETCH_ALL_PAGE_LIMIT = 200

/**
 * Collects every row for the given filters in 100-row pages, e.g. for CSV export. Stops at
 * `total`, on an empty page or after 200 pages (20,000 rows), silently truncating larger results.
 */
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

/**
 * A paginated collection a server table reads: the key its pages are cached under and how one page
 * is fetched for the table's params. Entities expose their admin lists as sources.
 */
export interface ServerTableSource<T, TParams> {
  readonly queryKey: readonly unknown[]
  readonly fetchPage: (params: TParams) => Promise<ServerTablePage<T>>
}

interface UseServerTableOptions<T, TParams> extends ServerTableSource<T, TParams> {
  readonly columns?: Record<string, ServerTableColumn<TParams>> | undefined
  readonly defaultSort?: { readonly field: string; readonly order?: 1 | -1 } | undefined
  readonly rows?: number | undefined
  readonly extraParams?: (() => Record<string, unknown>) | undefined
  readonly enabled?: (() => boolean) | undefined
}

function initialFilters<TParams>(
  columns: Record<string, ServerTableColumn<TParams>>,
): Record<string, ServerTableFilterState> {
  const filters: Record<string, ServerTableFilterState> = {}
  for (const key of Object.keys(columns)) filters[key] = { value: null }
  return filters
}

/**
 * Server-side paginated, sorted and filtered Element Plus table backed by TanStack Query.
 *
 * Builds `page`, `pageSize`, `sort` (`-field` for descending) and column filter params, keeps the
 * previous page visible while fetching, and returns ready-to-bind `tableProps`/`paginationProps`
 * plus event handlers. Sort, page-size and `extraParams` changes, `onFilter` and `clearFilters` go
 * back to the first page; an empty page beyond the end jumps to the last one. Narrow viewports get
 * a compact paginator. `fetchAll` collects every row matching the current filters and sort, e.g.
 * for CSV export.
 */
export function useServerTable<T, TParams = Record<string, unknown>>(
  options: UseServerTableOptions<T, TParams>,
) {
  const columns = options.columns ?? {}
  const first = ref(0)
  const rows = ref(options.rows ?? 25)
  const sortField = ref<string | undefined>(options.defaultSort?.field)
  const sortOrder = ref<number>(options.defaultSort?.order ?? 1)
  const filters = ref<Record<string, ServerTableFilterState>>(initialFilters(columns))
  const extra = computed<Record<string, unknown>>(() => options.extraParams?.() ?? {})
  const narrow = useMediaQuery(NARROW_QUERY)

  watch(extra, () => {
    first.value = 0
  })

  const filterParams = computed<Record<string, unknown>>(() => {
    const result: Record<string, unknown> = { ...extra.value }

    for (const [key, column] of Object.entries(columns)) {
      const value = filters.value[key]?.value
      if (value === null || value === undefined || value === '') continue
      if (column.type === 'dateRange') {
        const range: readonly unknown[] = Array.isArray(value) ? value : []
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

  const defaultSortProp = computed(() => sortField.value ?? '')
  const defaultSortOrder = computed<ServerTableSortOrder>(() =>
    sortOrder.value === -1 ? 'descending' : 'ascending',
  )

  const defaultSort = computed<ServerTableDefaultSort>(() => ({
    prop: defaultSortProp.value,
    order: defaultSortOrder.value,
  }))

  const tableProps = computed(() => ({
    data: page.value.items,
    rowKey: 'id',
    stripe: true,
    defaultSort: defaultSort.value,
    scrollbarAlwaysOn: narrow.value,
  }))

  const paginationProps = computed(() => ({
    currentPage: Math.floor(first.value / rows.value) + 1,
    pageSize: rows.value,
    total: page.value.total,
    pageSizes: ROWS_PER_PAGE_OPTIONS,
    layout: narrow.value ? PAGINATION_LAYOUT_NARROW : PAGINATION_LAYOUT,
    pagerCount: narrow.value ? 5 : 7,
    background: true,
  }))

  function onSortChange(event: ServerTableSortChange): void {
    const order = event.order
    sortField.value = order === null || typeof event.prop !== 'string' ? undefined : event.prop
    sortOrder.value = order === 'descending' ? -1 : 1
    first.value = 0
  }

  function onCurrentPageChange(nextPage: number): void {
    first.value = Math.max(0, nextPage - 1) * rows.value
  }

  function onPageSizeChange(size: number): void {
    rows.value = size
    first.value = 0
  }

  function onFilter(): void {
    first.value = 0
  }

  function clearFilters(): void {
    for (const meta of Object.values(filters.value)) meta.value = null
    onFilter()
  }

  function fetchAll(): Promise<T[]> {
    return fetchAllPages((next) => options.fetchPage(next as unknown as TParams), {
      ...filterParams.value,
      sort: sortParam.value,
    })
  }

  function columnFilter(key: string): ServerTableFilter {
    const existing = filters.value[key]
    if (existing) return existing as ServerTableFilter
    const created: ServerTableFilterState = { value: null }
    filters.value[key] = created
    return (filters.value[key] ?? created) as ServerTableFilter
  }

  return {
    tableProps,
    paginationProps,
    defaultSort,
    defaultSortProp,
    defaultSortOrder,
    items: computed(() => page.value.items),
    total: computed(() => page.value.total),
    loading: tableQuery.isFetching,
    isError: tableQuery.isError,
    isNarrow: narrow,
    first,
    rows,
    sortField,
    sortOrder,
    filterParams,
    sortParam,
    columnFilter,
    clearFilters,
    fetchAll,
    onSortChange,
    onCurrentPageChange,
    onPageSizeChange,
    onFilter,
  }
}

/** State and handlers of a server-side table, as `useServerTable` returns them. */
export type ServerTable<T> = ReturnType<typeof useServerTable<T>>
