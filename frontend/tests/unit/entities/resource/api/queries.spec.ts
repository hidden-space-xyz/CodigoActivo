import { ref } from 'vue'
import { describe, expect, it, vi } from 'vitest'

import { useResourceDetail, useResources } from '@/entities/resource'
import type { ResourceResponse } from '@/shared/api/generated/models'

import { renderComposable } from '../../../../support/fixtures/entities/composable'
import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'

describe('useResources', () => {
  it('loads resources for the search term, reloads when it changes and pages on demand', async () => {
    const queries: Record<string, string>[] = []
    server.use(
      http.get('/api/resources', ({ request }) => {
        const query = Object.fromEntries(new URL(request.url).searchParams)
        queries.push(query)
        const id = `${query.search ?? 'all'}-${query.page ?? ''}`
        return HttpResponse.json(paged([{ id }], query.search ? 2 : 1))
      }),
    )
    const search = ref('')

    const { result } = await renderComposable(() => useResources(search))
    await vi.waitFor(() => expect(result.resources.value.map((r) => r.id)).toEqual(['all-1']))
    expect(result.hasMore.value).toBe(false)
    expect(result.isLoading.value).toBe(false)

    search.value = 'python'
    await vi.waitFor(() => expect(result.resources.value.map((r) => r.id)).toEqual(['python-1']))
    expect(result.hasMore.value).toBe(true)

    result.loadMore()
    await vi.waitFor(() =>
      expect(result.resources.value.map((r) => r.id)).toEqual(['python-1', 'python-2']),
    )
    expect(result.isFetchingMore.value).toBe(false)
    expect(result.isError.value).toBe(false)
    expect(queries.map((query) => [query.search, query.page])).toEqual([
      [undefined, '1'],
      ['python', '1'],
      ['python', '2'],
    ])
  })

  it('reports an error', async () => {
    server.use(http.get('/api/resources', () => apiError(500)))

    const { result } = await renderComposable(() => useResources(''))

    await vi.waitFor(() => expect(result.isError.value).toBe(true))
  })
})

describe('useResourceDetail', () => {
  it('loads the resource for a reactive id', async () => {
    server.use(
      http.get('/api/resources/:id', ({ params }) =>
        HttpResponse.json<ResourceResponse>({ id: String(params.id), title: String(params.id) }),
      ),
    )
    const id = ref('r1')

    const { result } = await renderComposable(() => useResourceDetail(id))
    await vi.waitFor(() => expect(result.resource.value?.title).toBe('r1'))
    expect(result.notFound.value).toBe(false)

    id.value = 'r2'
    await vi.waitFor(() => expect(result.resource.value?.title).toBe('r2'))
  })

  it('flags a missing resource as not found', async () => {
    server.use(http.get('/api/resources/missing', () => apiError(404)))

    const { result } = await renderComposable(() => useResourceDetail('missing'))

    await vi.waitFor(() => expect(result.notFound.value).toBe(true))
    expect(result.isError.value).toBe(false)
  })
})
