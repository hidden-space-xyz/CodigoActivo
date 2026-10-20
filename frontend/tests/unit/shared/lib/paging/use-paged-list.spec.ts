import { flushPromises } from '@vue/test-utils'
import { ref } from 'vue'
import { describe, expect, it, vi } from 'vitest'

import { mountComposable } from '../../../../support/render'
import { type PagedListPage, usePagedList } from '@/shared/lib/paging'

function pageOf(start: number, count: number, total: number): PagedListPage<number> {
  return { items: Array.from({ length: count }, (_, index) => start + index), total }
}

describe('usePagedList', () => {
  it('loads the first page with the default page size', async () => {
    const fetchPage = vi.fn((page: number, pageSize: number) =>
      Promise.resolve(pageOf((page - 1) * pageSize, pageSize, 60)),
    )

    const { result } = await mountComposable(() =>
      usePagedList(() => ({ queryKey: ['numbers'], fetchPage })),
    )
    await vi.waitFor(() => expect(result.isLoading.value).toBe(false))

    expect(fetchPage).toHaveBeenCalledWith(1, 25)
    expect(result.items.value).toHaveLength(25)
    expect(result.hasMore.value).toBe(true)
  })

  it('appends pages on loadMore until every item is loaded', async () => {
    const fetchPage = vi.fn((page: number, pageSize: number) => {
      const start = (page - 1) * pageSize
      return Promise.resolve(pageOf(start, Math.min(pageSize, 60 - start), 60))
    })

    const { result } = await mountComposable(() =>
      usePagedList(() => ({ queryKey: ['small'], fetchPage })),
    )
    await vi.waitFor(() => expect(result.items.value).toHaveLength(25))

    result.loadMore()
    await vi.waitFor(() => expect(result.items.value).toHaveLength(50))
    expect(result.hasMore.value).toBe(true)

    result.loadMore()
    await vi.waitFor(() => expect(result.items.value).toHaveLength(60))

    expect(result.items.value.at(-1)).toBe(59)
    expect(fetchPage.mock.calls).toEqual([
      [1, 25],
      [2, 25],
      [3, 25],
    ])
    expect(result.hasMore.value).toBe(false)
    expect(result.isFetchingMore.value).toBe(false)
  })

  it('starts over from the first page when the source key changes', async () => {
    const term = ref('a')
    const fetchPage = vi.fn(() => Promise.resolve(pageOf(0, 1, 1)))

    await mountComposable(() =>
      usePagedList(() => ({ queryKey: ['search', term.value], fetchPage })),
    )
    await vi.waitFor(() => expect(fetchPage).toHaveBeenCalledTimes(1))

    term.value = 'b'
    await vi.waitFor(() => expect(fetchPage).toHaveBeenCalledTimes(2))
    expect(fetchPage).toHaveBeenLastCalledWith(1, 25)
  })

  it('does not fetch while disabled and starts once enabled', async () => {
    const enabled = ref(false)
    const fetchPage = vi.fn(() => Promise.resolve(pageOf(0, 0, 0)))

    const { result } = await mountComposable(() =>
      usePagedList(() => ({ queryKey: ['gated'], fetchPage }), { enabled: () => enabled.value }),
    )
    await flushPromises()

    expect(fetchPage).not.toHaveBeenCalled()
    expect(result.items.value).toEqual([])

    enabled.value = true
    await vi.waitFor(() => expect(fetchPage).toHaveBeenCalledTimes(1))
  })

  it('keeps the list empty and stops loading when a page fails', async () => {
    const { result } = await mountComposable(() =>
      usePagedList(() => ({
        queryKey: ['broken'],
        fetchPage: () => Promise.reject(new Error('Boom')),
      })),
    )

    await vi.waitFor(() => expect(result.isLoading.value).toBe(false))
    expect(result.items.value).toEqual([])
    expect(result.hasMore.value).toBe(false)
  })
})
