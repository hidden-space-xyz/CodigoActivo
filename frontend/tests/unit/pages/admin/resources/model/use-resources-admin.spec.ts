import { describe, expect, it, vi } from 'vitest'

import type { ResourceInput } from '@/entities/resource'
import { useResourcesAdmin } from '@/pages/admin/resources/model/use-resources-admin'

import { apiError, http, HttpResponse, paged, server } from '../../../../../support/server'
import {
  buildResourceListItem,
  buildResourceResponse,
  internalType,
} from '../../../../../support/builders'
import { expectNotification, queryOf } from '../../../../../support/dom'
import { buildResourceSummary } from '../../../../../support/models'
import { mountComposable, t } from '../../../../../support/render'

const INPUT: ResourceInput = {
  title: 'Vue',
  subtitle: 'Docs',
  description: null,
  url: 'https://vuejs.org',
  resourceTypeId: 'type-link',
  thumbnailId: 'thumb',
}

function serveList() {
  const urls: string[] = []
  server.use(
    http.get('/api/resources', ({ request }) => {
      urls.push(request.url)
      return HttpResponse.json(paged([buildResourceListItem()]))
    }),
    http.get('/api/resources/types', () => HttpResponse.json([internalType])),
  )
  return urls
}

describe('useResourcesAdmin', () => {
  it('requests the newest resources first and loads the types for the filter', async () => {
    const urls = serveList()

    const { result } = await mountComposable(() => useResourcesAdmin())

    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: '-createdAt' })
    await vi.waitFor(() => expect(result.types.data.value).toHaveLength(1))
  })

  it('searches title and subtitle with one term and filters by type, url and creation date', async () => {
    const urls = serveList()
    const { result } = await mountComposable(() => useResourcesAdmin())
    await vi.waitFor(() => expect(urls).toHaveLength(1))

    result.table.columnFilter('search').value = 'python'
    result.table.columnFilter('type').value = 'type-article'
    result.table.columnFilter('url').value = 'python.org'
    result.table.columnFilter('created').value = ['2026-01-01', '2026-01-31']
    result.table.onFilter()

    await vi.waitFor(() =>
      expect(queryOf(urls.at(-1) ?? '')).toEqual({
        page: '1',
        pageSize: '25',
        sort: '-createdAt',
        search: 'python',
        resourceTypeId: 'type-article',
        url: 'python.org',
        createdFrom: '2026-01-01',
        createdTo: '2026-01-31',
      }),
    )
    expect(result.table.hasActiveFilters.value).toBe(true)
  })

  it('loads the whole resource before editing it and reports one that no longer exists', async () => {
    serveList()
    server.use(
      http.get('/api/resources/:id', ({ params }) =>
        params.id === 'missing' ? apiError(404) : HttpResponse.json(buildResourceResponse()),
      ),
    )
    const { result } = await mountComposable(() => useResourcesAdmin())

    await result.dialog.openEdit(buildResourceSummary({ id: 'resource-1' }))
    expect(result.dialog.editing.value?.description).toBe(buildResourceResponse().description)

    result.dialog.close()
    await result.dialog.openEdit(buildResourceSummary({ id: 'missing' }))
    expect(result.dialog.visible.value).toBe(false)
    await expectNotification(t('pages.admin.resources.toasts.notFound'))
  })

  it('creates a resource when none is edited and replaces the edited one', async () => {
    serveList()
    const calls: string[] = []
    server.use(
      http.get('/api/resources/:id', () => HttpResponse.json(buildResourceResponse())),
      http.post('/api/resources', () => {
        calls.push('POST')
        return HttpResponse.json(buildResourceResponse(), { status: 201 })
      }),
      http.put('/api/resources/:id', ({ params }) => {
        calls.push(`PUT ${String(params.id)}`)
        return HttpResponse.json(buildResourceResponse())
      }),
    )
    const { result } = await mountComposable(() => useResourcesAdmin())

    result.dialog.openCreate()
    result.save(INPUT)
    await expectNotification(t('pages.admin.resources.toasts.created'))
    expect(result.dialog.visible.value).toBe(false)

    await result.dialog.openEdit(buildResourceSummary())
    result.save(INPUT)
    await expectNotification(t('pages.admin.resources.toasts.updated'))

    expect(calls).toEqual(['POST', 'PUT resource-1'])
  })
})
