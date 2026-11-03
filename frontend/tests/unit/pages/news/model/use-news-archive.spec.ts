import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { newsKeys } from '@/entities/news-item'
import { useNewsArchive } from '@/pages/news/model/use-news-archive'

import { http, HttpResponse, paged, server } from '../../../../support/server'
import { mountComposable } from '../../../../support/render'

describe('useNewsArchive', () => {
  it('keeps a selected year that is still available and resets one that disappears', async () => {
    let years = [2026, 2025]
    server.use(
      http.get('/api/news/years', () => HttpResponse.json(years)),
      http.get('/api/news', () => HttpResponse.json(paged([]))),
    )

    const { result, queryClient } = await mountComposable(() => useNewsArchive())
    await vi.waitFor(() => expect(result.selectedYear.value).toBe('2026'))
    result.setYear('2025')

    years = [2027, 2025]
    await queryClient.refetchQueries({ queryKey: newsKeys.years() })
    await flushPromises()
    expect(result.selectedYear.value).toBe('2025')

    years = [2027]
    await queryClient.refetchQueries({ queryKey: newsKeys.years() })
    await vi.waitFor(() => expect(result.selectedYear.value).toBe('2027'))
  })
})
