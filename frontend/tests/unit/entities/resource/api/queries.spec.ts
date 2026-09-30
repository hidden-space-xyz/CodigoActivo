import { describe, expect, it } from 'vitest'

import { resourceKeys, resourceList, resourcePages, resourceQueries } from '@/entities/resource'

import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'
import {
  buildResourceListItem,
  buildResourceResponse,
  internalType,
} from '../../../../support/builders'
import { queryOf } from '../../../../support/dom'
import { createTestQueryClient } from '../../../../support/render'

describe('resourceKeys', () => {
  it('nests the resources under one root and keeps their types apart', () => {
    expect(resourceKeys.pages('python')).toEqual(['resources', 'pages', 'python'])
    expect(resourceKeys.detail('r1')).toEqual(['resources', 'detail', 'r1'])
    expect(resourceKeys.list()).toEqual(['resources', 'list'])
    expect(resourceKeys.types()).toEqual(['resource-types'])
  })
})

describe('resourceQueries', () => {
  it('loads a whole resource, or null when it does not exist', async () => {
    server.use(
      http.get('/api/resources/r1', () => HttpResponse.json(buildResourceResponse({ id: 'r1' }))),
      http.get('/api/resources/missing', () => apiError(404)),
    )
    const client = createTestQueryClient()

    await expect(client.fetchQuery(resourceQueries.detail('r1'))).resolves.toMatchObject({
      id: 'r1',
    })
    await expect(client.fetchQuery(resourceQueries.detail('missing'))).resolves.toBeNull()
  })

  it('loads the resource types', async () => {
    server.use(http.get('/api/resources/types', () => HttpResponse.json([internalType])))

    await expect(createTestQueryClient().fetchQuery(resourceQueries.types())).resolves.toEqual([
      expect.objectContaining({ id: 'type-article', isExternal: false }),
    ])
  })
})

describe('resource sources', () => {
  it('pages the public resources matching a search under its own key', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/resources', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildResourceListItem()], 3))
      }),
    )
    const source = resourcePages('python')

    await expect(source.fetchPage(2, 10)).resolves.toMatchObject({ total: 3 })
    expect(source.queryKey).toEqual(resourceKeys.pages('python'))
    expect(queryOf(urls[0] ?? '')).toEqual({
      search: 'python',
      sort: '-createdAt',
      page: '2',
      pageSize: '10',
    })
  })

  it('pages the admin list under the list key', async () => {
    server.use(
      http.get('/api/resources', () => HttpResponse.json(paged([buildResourceListItem()], 1))),
    )

    await expect(resourceList.fetchPage({ page: 1 })).resolves.toMatchObject({ total: 1 })
    expect(resourceList.queryKey).toEqual(resourceKeys.list())
  })
})
