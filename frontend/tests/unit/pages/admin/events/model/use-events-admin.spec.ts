import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useEventsAdmin } from '@/pages/admin/events/model/use-events-admin'

import { http, HttpResponse, paged, server } from '../../../../../support/server'
import { buildEventListItem, buildEventResponse } from '../../../../../support/builders'
import { expectNotification, queryOf } from '../../../../../support/dom'
import { buildEventListing } from '../../../../../support/models'
import { mountComposable, t } from '../../../../../support/render'

describe('useEventsAdmin', () => {
  it('switches the scope with its sort and goes back to the first page', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/events', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildEventListItem()], 80))
      }),
    )

    const { result } = await mountComposable(() => useEventsAdmin())
    await vi.waitFor(() => expect(urls).toHaveLength(1))

    result.table.onCurrentPageChange(3)
    await vi.waitFor(() => expect(queryOf(urls.at(-1) ?? '').page).toBe('3'))

    result.setScope('past')
    await vi.waitFor(() =>
      expect(queryOf(urls.at(-1) ?? '')).toEqual({
        page: '1',
        pageSize: '25',
        sort: '-eventStartsAt',
        scope: 'Past',
      }),
    )
    expect(result.scope.value).toBe('past')

    result.setScope('all')
    await vi.waitFor(() =>
      expect(queryOf(urls.at(-1) ?? '')).toEqual({
        page: '1',
        pageSize: '25',
        sort: '-eventStartsAt',
      }),
    )

    result.setScope('upcoming')
    await vi.waitFor(() =>
      expect(queryOf(urls.at(-1) ?? '')).toEqual({
        page: '1',
        pageSize: '25',
        sort: 'eventStartsAt',
        scope: 'Upcoming',
      }),
    )
    expect(result.table.sortOrder.value).toBe(1)
  })

  it('features only an event that is not featured yet', async () => {
    const featured: string[] = []
    server.use(
      http.get('/api/events', () => HttpResponse.json(paged([]))),
      http.patch('/api/events/:id/feature', ({ params }) => {
        featured.push(String(params.id))
        return HttpResponse.json(buildEventResponse({ featured: true }))
      }),
    )
    const { result } = await mountComposable(() => useEventsAdmin())

    result.feature(buildEventListing({ id: 'top', featured: true }))
    await flushPromises()
    result.feature(buildEventListing({ id: 'other' }))
    await expectNotification(t('pages.admin.events.toasts.featured'))

    expect(featured).toEqual(['other'])
  })
})
