import { computed, ref, watch } from 'vue'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'

import { toDateOnly } from './format'
import { useMediaQuery } from './use-theme'

export type ServerTableFieldType = 'text' | 'number' | 'dateRange'
export type ServerTableSortOrder = 'ascending' | 'descending'

const ROWS_PER_PAGE_OPTIONS = [25, 50, 100]
const PAGINATION_LAYOUT = 'total, sizes, prev, pager, next'
const PAGINATION_LAYOUT_NARROW = 'prev, pager, next'
const NARROW_QUERY = '(max-width: 640px)'
const DATE_ONLY_PATTERN = /^\d{4}-\d{2}-\d{2}$/

export interface ServerTableColumn<TParams = Record<string, unknown>> {
  readonly param?: Extract<keyof TParams, string>
  readonly type?: ServerTableFieldType
  readonly fromParam?: Extract<keyof TParams, string>
  readonly toParam?: Extract<keyof TParams, string>
}

export type ServerTableFilterValue =
  string | number | boolean | Date | readonly (Date | string | null)[] | null | undefined

export interface ServerTableFilter {
  get value(): never
  set value(next: ServerTableFilterValue)
}

export interface ServerTableSortChange {
  readonly prop: string | null
  readonly order: ServerTableSortOrder | null
}

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
    // params is stitched together from column keys and extraParams at runtime, so its shape
    // cannot be proven to match TParams; explicit param names are still checked via the
    // ServerTableColumn<TParams> constraint.
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
    onSortChange,
    onCurrentPageChange,
    onPageSizeChange,
    onFilter,
  }
}
