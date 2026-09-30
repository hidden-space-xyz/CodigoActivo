import { ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useEventRatings } from '@/pages/admin/event-detail/model/use-event-ratings'

import { http, HttpResponse, paged, server } from '../../../../../support/server'
import { buildRating } from '../../../../../support/builders'
import { queryOf } from '../../../../../support/dom'
import { mountComposable } from '../../../../../support/render'

describe('useEventRatings', () => {
  it('lists the ratings of the event best first, only while the tab is active', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/events/:eventId/ratings', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildRating()]))
      }),
    )
    const active = ref(false)

    const { result } = await mountComposable(() =>
      useEventRatings(
        () => 'event-1',
        () => active.value,
      ),
    )
    await flushPromises()
    expect(urls).toEqual([])

    active.value = true
    await vi.waitFor(() => expect(result.items.value).toHaveLength(1))
    expect(new URL(urls[0] ?? '').pathname).toBe('/api/events/event-1/ratings')
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: '-score' })
  })
})
