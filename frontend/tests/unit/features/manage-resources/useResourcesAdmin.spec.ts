import { describe, expect, it, vi } from 'vitest'

import { useResourcesAdmin } from '@/features/manage-resources'

import { buildResource } from '../../../support/fixtures/admin-content/builders'
import { queryOf, withSetup } from '../../../support/fixtures/admin-content/helpers'
import { apiError, http, HttpResponse, paged, server } from '../../../support/server'

describe('useResourcesAdmin', () => {
  it('requests resources newest first', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/resources', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildResource()]))
      }),
    )
    const { result } = await withSetup(() => useResourcesAdmin())

    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: '-createdAt' })
  })

  it('loads a resource detail, resolving null when it does not exist', async () => {
    server.use(
      http.get('/api/resources', () => HttpResponse.json(paged([]))),
      http.get('/api/resources/:id', ({ params }) =>
        params.id === 'missing' ? apiError(404) : HttpResponse.json(buildResource()),
      ),
    )
    const { result } = await withSetup(() => useResourcesAdmin())

    await expect(result.fetchOne('resource-1')).resolves.toMatchObject({ title: 'Vue guide' })
    await expect(result.fetchOne('missing')).resolves.toBeNull()
  })

  it('creates, updates and deletes resources and invalidates resource queries', async () => {
    const requests: string[] = []
    server.use(
      http.get('/api/resources', () => HttpResponse.json(paged([]))),
      http.post('/api/resources', () => {
        requests.push('POST')
        return HttpResponse.json(buildResource(), { status: 201 })
      }),
      http.put('/api/resources/:id', ({ params }) => {
        requests.push(`PUT ${String(params.id)}`)
        return HttpResponse.json(buildResource())
      }),
      http.delete('/api/resources/:id', ({ params }) => {
        requests.push(`DELETE ${String(params.id)}`)
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { result, queryClient } = await withSetup(() => useResourcesAdmin())
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
    const body = {
      title: 'Vue',
      subtitle: 'Docs',
      description: null,
      url: 'https://vuejs.org',
      resourceTypeId: 'type-link',
      thumbnailId: 'thumb',
    }

    await result.create.mutateAsync(body)
    await result.update.mutateAsync({ id: 'resource-1', body })
    await result.remove.mutateAsync('resource-1')

    expect(requests).toEqual(['POST', 'PUT resource-1', 'DELETE resource-1'])
    expect(invalidate).toHaveBeenCalledTimes(3)
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['resources'] })
  })
})
