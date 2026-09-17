import { describe, expect, it, vi } from 'vitest'

import { partnerQueryKeys, useSponsors } from '@/entities/partner'

import { renderComposable } from '../../../../support/fixtures/entities/composable'
import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'

describe('useSponsors', () => {
  it('keeps sponsors undefined until loaded and then exposes them', async () => {
    server.use(
      http.get('/api/partners', () => HttpResponse.json(paged([{ id: 'p1', name: 'Acme' }]))),
    )

    const { result, queryClient } = await renderComposable(() => {
      const sponsors = useSponsors()
      expect(sponsors.sponsors.value).toBeUndefined()
      expect(sponsors.isLoading.value).toBe(true)
      return sponsors
    })

    await vi.waitFor(() =>
      expect(result.sponsors.value).toEqual([
        { id: 'p1', name: 'Acme', website: '', thumbnailId: '' },
      ]),
    )
    expect(result.isLoading.value).toBe(false)
    expect(queryClient.getQueryData(partnerQueryKeys.sponsors())).toEqual(result.sponsors.value)
  })

  it('reports an error', async () => {
    server.use(http.get('/api/partners', () => apiError(503)))

    const { result } = await renderComposable(() => useSponsors())

    await vi.waitFor(() => expect(result.isError.value).toBe(true))
  })
})
