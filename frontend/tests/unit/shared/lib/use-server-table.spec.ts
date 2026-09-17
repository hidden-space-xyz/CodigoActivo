import { flushPromises } from '@vue/test-utils'
import { nextTick, ref } from 'vue'
import { describe, expect, it, vi } from 'vitest'

import { fetchAllPages, useServerTable, type ServerTablePage } from '@/shared/lib'

import { withSetup } from '../../../support/fixtures/shared-app/with-setup'
import { setMediaQueryMatches } from '../../../support/media'

interface Row {
  id: number
}

type Params = Record<string, unknown>

function rows(count: number, start = 0): Row[] {
  return Array.from({ length: count }, (_, index) => ({ id: start + index }))
}

function fixedPage(total: number, count = total) {
  return vi.fn<(params: Params) => Promise<ServerTablePage<Row>>>(() =>
    Promise.resolve<ServerTablePage<Row>>({ items: rows(count), total }),
  )
}

function lastParams(fetchPage: ReturnType<typeof fixedPage>): Params | undefined {
  return fetchPage.mock.lastCall?.[0]
}

describe('useServerTable', () => {
  it('requests the first page with default size and sort and exposes bindable props', async () => {
    const fetchPage = fixedPage(3)

    const { result } = await withSetup(() =>
      useServerTable<Row>({
        queryKey: ['rows'],
        fetchPage,
        defaultSort: { field: 'createdAt', order: -1 },
        extraParams: () => ({ status: 'active' }),
      }),
    )
    await vi.waitFor(() => expect(result.items.value).toHaveLength(3))

    expect(lastParams(fetchPage)).toEqual({
      page: 1,
      pageSize: 25,
      status: 'active',
      sort: '-createdAt',
    })
    expect(result.total.value).toBe(3)
    expect(result.loading.value).toBe(false)
    expect(result.isError.value).toBe(false)
    expect(result.tableProps.value).toEqual({
      data: rows(3),
      rowKey: 'id',
      stripe: true,
      defaultSort: { prop: 'createdAt', order: 'descending' },
      scrollbarAlwaysOn: false,
    })
    expect(result.paginationProps.value).toEqual({
      currentPage: 1,
      pageSize: 25,
      total: 3,
      pageSizes: [25, 50, 100],
      layout: 'total, sizes, prev, pager, next',
      pagerCount: 7,
      background: true,
    })
    expect(result.defaultSortProp.value).toBe('createdAt')
    expect(result.defaultSortOrder.value).toBe('descending')
  })

  it('omits the sort without a default and uses ascending order by default', async () => {
    const fetchPage = fixedPage(0)

    const { result } = await withSetup(() =>
      useServerTable<Row>({ queryKey: ['unsorted'], fetchPage, rows: 50 }),
    )
    await vi.waitFor(() => expect(fetchPage).toHaveBeenCalled())

    expect(lastParams(fetchPage)).toEqual({ page: 1, pageSize: 50 })
    expect(result.sortParam.value).toBeUndefined()
    expect(result.defaultSort.value).toEqual({ prop: '', order: 'ascending' })
    expect(result.items.value).toEqual([])
  })

  it('maps text, number and date range filters to query parameters', async () => {
    const fetchPage = fixedPage(1)

    const { result } = await withSetup(() =>
      useServerTable<Row>({
        queryKey: ['filtered'],
        fetchPage,
        columns: {
          name: {},
          email: { param: 'emailContains' },
          age: { type: 'number' },
          count: { type: 'number' },
          createdAt: { type: 'dateRange' },
          signup: { type: 'dateRange', fromParam: 'signupAfter', toParam: 'signupBefore' },
          empty: {},
        },
      }),
    )

    result.columnFilter('name').value = 'ada'
    result.columnFilter('email').value = 'example'
    result.columnFilter('age').value = '42'
    result.columnFilter('count').value = 'many'
    result.columnFilter('createdAt').value = [new Date(2025, 0, 5), '2025-02-10']
    result.columnFilter('signup').value = ['2025-03-01T10:00:00', new Date('invalid')]
    result.columnFilter('empty').value = ''
    result.onFilter()
    await nextTick()

    expect(result.filterParams.value).toEqual({
      name: 'ada',
      emailContains: 'example',
      age: 42,
      createdAtFrom: '2025-01-05',
      createdAtTo: '2025-02-10',
      signupAfter: '2025-03-01',
    })
    await vi.waitFor(() =>
      expect(lastParams(fetchPage)).toEqual({
        page: 1,
        pageSize: 25,
        ...result.filterParams.value,
      }),
    )
  })

  it('ignores date range values that are not ranges or not dates', async () => {
    const { result } = await withSetup(() =>
      useServerTable<Row>({
        queryKey: ['dates'],
        fetchPage: fixedPage(0),
        columns: { period: { type: 'dateRange' }, other: { type: 'dateRange' } },
      }),
    )

    result.columnFilter('period').value = 'not-a-range'
    result.columnFilter('other').value = ['garbage', '', null]

    expect(result.filterParams.value).toEqual({})
  })

  it('creates filters for unknown columns on demand and reuses them', async () => {
    const { result } = await withSetup(() =>
      useServerTable<Row>({ queryKey: ['adhoc'], fetchPage: fixedPage(0) }),
    )

    const filter = result.columnFilter('dynamic')

    expect(result.columnFilter('dynamic')).toBe(filter)
  })

  it('updates sorting from table events and returns to the first page', async () => {
    const fetchPage = fixedPage(200, 25)
    const { result } = await withSetup(() =>
      useServerTable<Row>({ queryKey: ['sorting'], fetchPage }),
    )
    result.onCurrentPageChange(3)

    result.onSortChange({ prop: 'name', order: 'descending' })
    expect(result.sortParam.value).toBe('-name')
    expect(result.first.value).toBe(0)

    result.onSortChange({ prop: 'name', order: 'ascending' })
    expect(result.sortParam.value).toBe('name')

    result.onSortChange({ prop: 'name', order: null })
    expect(result.sortField.value).toBeUndefined()

    result.onSortChange({ prop: null, order: 'ascending' })
    expect(result.sortParam.value).toBeUndefined()
    expect(result.sortOrder.value).toBe(1)
  })

  it('pages, resizes and clears filters', async () => {
    const fetchPage = fixedPage(300, 25)
    const { result } = await withSetup(() =>
      useServerTable<Row>({ queryKey: ['paging'], fetchPage, columns: { name: {} } }),
    )
    await vi.waitFor(() => expect(result.total.value).toBe(300))

    result.onCurrentPageChange(3)
    await vi.waitFor(() => expect(lastParams(fetchPage)).toMatchObject({ page: 3, pageSize: 25 }))
    expect(result.paginationProps.value.currentPage).toBe(3)

    result.onCurrentPageChange(0)
    expect(result.first.value).toBe(0)

    result.onCurrentPageChange(2)
    result.onPageSizeChange(100)
    expect(result.first.value).toBe(0)
    await vi.waitFor(() => expect(lastParams(fetchPage)).toMatchObject({ page: 1, pageSize: 100 }))

    result.columnFilter('name').value = 'x'
    result.onCurrentPageChange(2)
    result.clearFilters()
    expect(result.filterParams.value).toEqual({})
    expect(result.first.value).toBe(0)
  })

  it('returns to the first page when the extra parameters change', async () => {
    const status = ref('active')
    const fetchPage = fixedPage(100, 25)
    const { result } = await withSetup(() =>
      useServerTable<Row>({
        queryKey: ['extra'],
        fetchPage,
        extraParams: () => ({ status: status.value }),
      }),
    )
    result.onCurrentPageChange(4)

    status.value = 'archived'
    await nextTick()

    expect(result.first.value).toBe(0)
    await vi.waitFor(() =>
      expect(lastParams(fetchPage)).toEqual({ page: 1, pageSize: 25, status: 'archived' }),
    )
  })

  it('jumps to the last page when a page beyond the end comes back empty', async () => {
    const fetchPage = vi.fn((params: Params) =>
      Promise.resolve<ServerTablePage<Row>>(
        params.page === 1 ? { items: rows(25), total: 30 } : { items: [], total: 30 },
      ),
    )
    const { result } = await withSetup(() =>
      useServerTable<Row>({ queryKey: ['shrinking'], fetchPage }),
    )
    await vi.waitFor(() => expect(result.items.value).toHaveLength(25))

    result.onCurrentPageChange(5)

    await vi.waitFor(() => expect(result.first.value).toBe(25))
  })

  it('does not fetch while disabled', async () => {
    const fetchPage = fixedPage(1)

    await withSetup(() =>
      useServerTable<Row>({ queryKey: ['disabled'], fetchPage, enabled: () => false }),
    )
    await flushPromises()

    expect(fetchPage).not.toHaveBeenCalled()
  })

  it('exposes fetch failures', async () => {
    const { result } = await withSetup(() =>
      useServerTable<Row>({
        queryKey: ['failing'],
        fetchPage: () => Promise.reject(new Error('Boom')),
      }),
    )

    await vi.waitFor(() => expect(result.isError.value).toBe(true))
  })

  it('uses a compact paginator on narrow viewports', async () => {
    setMediaQueryMatches((query) => query === '(max-width: 640px)')

    const { result } = await withSetup(() =>
      useServerTable<Row>({ queryKey: ['narrow'], fetchPage: fixedPage(0) }),
    )

    expect(result.isNarrow.value).toBe(true)
    expect(result.paginationProps.value.layout).toBe('prev, pager, next')
    expect(result.paginationProps.value.pagerCount).toBe(5)
    expect(result.tableProps.value.scrollbarAlwaysOn).toBe(true)
  })
})

describe('fetchAllPages', () => {
  it('collects pages of 100 rows until the total is reached', async () => {
    const fetchPage = vi.fn((params: Params) => {
      const page = params.page as number
      return Promise.resolve({ items: rows(page < 3 ? 100 : 50, (page - 1) * 100), total: 250 })
    })

    const result = await fetchAllPages(fetchPage, { search: 'x' })

    expect(result).toHaveLength(250)
    expect(fetchPage.mock.calls.map(([params]) => params)).toEqual([
      { search: 'x', page: 1, pageSize: 100 },
      { search: 'x', page: 2, pageSize: 100 },
      { search: 'x', page: 3, pageSize: 100 },
    ])
  })

  it('stops on an empty page', async () => {
    const fetchPage = vi.fn((params: Params) =>
      Promise.resolve({ items: params.page === 1 ? rows(100) : [], total: 1000 }),
    )

    const result = await fetchAllPages(fetchPage, {})

    expect(result).toHaveLength(100)
    expect(fetchPage).toHaveBeenCalledTimes(2)
  })

  it('stops after 200 pages', async () => {
    const fetchPage = vi.fn(() => Promise.resolve({ items: [{ id: 1 }], total: 1_000_000 }))

    const result = await fetchAllPages(fetchPage, {})

    expect(fetchPage).toHaveBeenCalledTimes(200)
    expect(result).toHaveLength(200)
  })
})
