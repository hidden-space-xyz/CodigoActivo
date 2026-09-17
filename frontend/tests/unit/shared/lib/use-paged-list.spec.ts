import { flushPromises } from '@vue/test-utils'
import { ref } from 'vue'
import { describe, expect, it, vi } from 'vitest'

import { usePagedList, type PagedListPage } from '@/shared/lib'

import { withSetup } from '../../../support/fixtures/shared-app/with-setup'

function pageOf(start: number, count: number, total: number): PagedListPage<number> {
  return { items: Array.from({ length: count }, (_, index) => start + index), total }
}

describe('usePagedList', () => {
  it('loads the first page with the default page size', async () => {
    const fetchPage = vi.fn((page: number, pageSize: number) =>
      Promise.resolve(pageOf((page - 1) * pageSize, pageSize, 60)),
    )

    const { result } = await withSetup(() =>
      usePagedList({ queryKey: () => ['numbers'], fetchPage }),
    )
    await vi.waitFor(() => expect(result.isLoading.value).toBe(false))

    expect(fetchPage).toHaveBeenCalledWith(1, 25)
    expect(result.items.value).toHaveLength(25)
    expect(result.total.value).toBe(60)
    expect(result.hasMore.value).toBe(true)
    expect(result.isError.value).toBe(false)
  })

  it('appends pages on loadMore until every item is loaded', async () => {
    const fetchPage = vi.fn((page: number, pageSize: number) => {
      const start = (page - 1) * pageSize
      return Promise.resolve(pageOf(start, Math.min(pageSize, 5 - start), 5))
    })

    const { result } = await withSetup(() =>
      usePagedList({ queryKey: () => ['small'], fetchPage, pageSize: 2 }),
    )
    await vi.waitFor(() => expect(result.items.value).toEqual([0, 1]))

    result.loadMore()
    await vi.waitFor(() => expect(result.items.value).toEqual([0, 1, 2, 3]))
    expect(result.hasMore.value).toBe(true)

    result.loadMore()
    await vi.waitFor(() => expect(result.items.value).toEqual([0, 1, 2, 3, 4]))

    expect(fetchPage.mock.calls).toEqual([
      [1, 2],
      [2, 2],
      [3, 2],
    ])
    expect(result.hasMore.value).toBe(false)
    expect(result.isFetchingMore.value).toBe(false)
  })

  it('refetches from the first page when the query key changes', async () => {
    const term = ref('a')
    const fetchPage = vi.fn(() => Promise.resolve(pageOf(0, 1, 1)))

    await withSetup(() => usePagedList({ queryKey: () => ['search', term.value], fetchPage }))
    await vi.waitFor(() => expect(fetchPage).toHaveBeenCalledTimes(1))

    term.value = 'b'
    await vi.waitFor(() => expect(fetchPage).toHaveBeenCalledTimes(2))
    expect(fetchPage).toHaveBeenLastCalledWith(1, 25)
  })

  it('does not fetch while disabled and starts once enabled', async () => {
    const enabled = ref(false)
    const fetchPage = vi.fn(() => Promise.resolve(pageOf(0, 0, 0)))

    const { result } = await withSetup(() =>
      usePagedList({ queryKey: () => ['gated'], fetchPage, enabled: () => enabled.value }),
    )
    await flushPromises()

    expect(fetchPage).not.toHaveBeenCalled()
    expect(result.items.value).toEqual([])
    expect(result.total.value).toBe(0)

    enabled.value = true
    await vi.waitFor(() => expect(fetchPage).toHaveBeenCalledTimes(1))
  })

  it('exposes failures through isError', async () => {
    const { result } = await withSetup(() =>
      usePagedList({
        queryKey: () => ['broken'],
        fetchPage: () => Promise.reject(new Error('Boom')),
      }),
    )

    await vi.waitFor(() => expect(result.isError.value).toBe(true))
    expect(result.items.value).toEqual([])
  })
})
