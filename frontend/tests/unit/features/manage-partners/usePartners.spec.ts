import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { usePartners } from '@/features/manage-partners'

import { buildPartner } from '../../../support/fixtures/admin-content/builders'
import { queryOf, withSetup } from '../../../support/fixtures/admin-content/helpers'
import { apiError, http, HttpResponse, paged, server } from '../../../support/server'

describe('usePartners', () => {
  it('requests partners by tier and converts tier filters to numbers', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/partners', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildPartner()]))
      }),
    )
    const { result } = await withSetup(() => usePartners())

    await vi.waitFor(() => expect(result.table.total.value).toBe(1))
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'tier' })

    result.table.columnFilter('tier').value = 'not a number'
    await flushPromises()
    expect(result.table.filterParams.value).toEqual({})

    result.table.columnFilter('tier').value = '3'
    await flushPromises()
    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({ tier: '3' })
  })

  it('creates, updates and deletes partners and invalidates partner queries on success', async () => {
    server.use(
      http.get('/api/partners', () => HttpResponse.json(paged([]))),
      http.post('/api/partners', async ({ request }) =>
        HttpResponse.json({ ...buildPartner(), ...((await request.json()) as object) }),
      ),
      http.put('/api/partners/:id', async ({ request, params }) =>
        HttpResponse.json({
          ...buildPartner(),
          id: String(params.id),
          ...((await request.json()) as object),
        }),
      ),
      http.delete('/api/partners/:id', () => new HttpResponse(null, { status: 204 })),
    )
    const { result, queryClient } = await withSetup(() => usePartners())
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
    const body = {
      name: 'Initech',
      tier: 2,
      website: null,
      fromDate: '2025-01-01',
      thumbnailId: 't',
    }

    await expect(result.create.mutateAsync(body)).resolves.toMatchObject({ name: 'Initech' })
    await expect(result.update.mutateAsync({ id: 'partner-5', body })).resolves.toMatchObject({
      id: 'partner-5',
    })
    await result.remove.mutateAsync('partner-5')

    expect(invalidate).toHaveBeenCalledTimes(3)
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['partners'] })
  })

  it('does not invalidate when a mutation fails', async () => {
    server.use(
      http.get('/api/partners', () => HttpResponse.json(paged([]))),
      http.delete('/api/partners/:id', () => apiError(500)),
    )
    const { result, queryClient } = await withSetup(() => usePartners())
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    await expect(result.remove.mutateAsync('partner-5')).rejects.toMatchObject({ status: 500 })

    expect(invalidate).not.toHaveBeenCalled()
  })
})
