import { describe, expect, it, vi } from 'vitest'

import { useContentEntity } from '@/widgets/content-entity-page'
import type { ContentItem } from '@/widgets/content-entity-page/model/use-content-entity'

import { withSetup } from '../../support/fixtures/admin-content/helpers'

function fakeApi(overrides: { feature?: (id: string) => Promise<unknown> } = {}) {
  const items: ContentItem[] = [{ id: 'item-1', title: 'First' }]
  return {
    queryKey: ['content'] as const,
    fetchPage: vi.fn((params: Record<string, unknown>) => {
      void params
      return Promise.resolve({ items, total: items.length })
    }),
    columns: { title: { type: 'text' as const } },
    defaultSort: { field: 'createdAt', order: -1 as const },
    fetchOne: vi.fn((id: string) => Promise.resolve<ContentItem | null>({ id })),
    create: vi.fn(() => Promise.resolve()),
    update: vi.fn(() => Promise.resolve()),
    remove: vi.fn(() => Promise.resolve()),
    ...overrides,
  }
}

describe('useContentEntity', () => {
  it('builds a server table with the given sort and columns', async () => {
    const api = fakeApi()
    const { result } = await withSetup(() => useContentEntity(api))

    await vi.waitFor(() =>
      expect(result.table.items.value).toEqual([{ id: 'item-1', title: 'First' }]),
    )
    expect(api.fetchPage).toHaveBeenCalledWith({ page: 1, pageSize: 25, sort: '-createdAt' })
    expect(result.fetchOne).toBe(api.fetchOne)
  })

  it('runs mutations through the api and invalidates the entity key', async () => {
    const feature = vi.fn(() => Promise.resolve())
    const api = fakeApi({ feature })
    const { result, queryClient } = await withSetup(() => useContentEntity(api))
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
    const body = { title: 'T', subtitle: 'S' }

    await result.create.mutateAsync(body)
    await result.update.mutateAsync({ id: 'item-1', body })
    await result.remove.mutateAsync('item-1')
    await result.feature.mutateAsync('item-1')

    expect(result.canFeature).toBe(true)
    expect(api.create).toHaveBeenCalledWith(body)
    expect(api.update).toHaveBeenCalledWith('item-1', body)
    expect(api.remove).toHaveBeenCalledWith('item-1')
    expect(feature).toHaveBeenCalledWith('item-1')
    expect(invalidate).toHaveBeenCalledTimes(4)
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['content'] })
  })

  it('reports that entities without a feature endpoint cannot be featured', async () => {
    const { result } = await withSetup(() => useContentEntity(fakeApi()))

    expect(result.canFeature).toBe(false)
    await expect(result.feature.mutateAsync('item-1')).resolves.toBeUndefined()
  })
})
