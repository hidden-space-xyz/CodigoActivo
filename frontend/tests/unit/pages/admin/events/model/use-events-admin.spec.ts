import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import type { EventInput } from '@/entities/event'
import { useEventsAdmin } from '@/pages/admin/events/model/use-events-admin'

import { http, HttpResponse, noContent, paged, server } from '../../../../../support/server'
import { buildEventListItem, buildEventResponse } from '../../../../../support/builders'
import { expectNotification, queryOf } from '../../../../../support/dom'
import { buildEventListing } from '../../../../../support/models'
import { mountComposable, t } from '../../../../../support/render'

const INPUT: EventInput = {
  title: 'New',
  subtitle: 'Sub',
  description: '{}',
  startsAt: '2026-10-10',
  endsAt: '2026-10-11',
  earlySignupStartsAt: null,
  signupStartsAt: '2026-09-01T00:00:00.000Z',
  signupEndsAt: '2026-09-30T00:00:00.000Z',
  thumbnailId: 'thumb',
  categoryIds: ['cat-1'],
  terms: [],
}

describe('useEventsAdmin', () => {
  it('requests the events by start date and sends the column filters', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/events', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildEventListItem()]))
      }),
    )

    const { result } = await mountComposable(() => useEventsAdmin())
    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'eventStartsAt' })

    result.table.columnFilter('search').value = 'hack'
    result.table.columnFilter('category').value = 'cat-2'
    result.table.columnFilter('eventDate').value = ['2026-10-01', '2026-10-31']
    result.table.columnFilter('signup').value = ['2026-09-01', null]
    await flushPromises()

    await vi.waitFor(() => expect(urls.length).toBeGreaterThan(1))
    expect(queryOf(urls.at(-1) ?? '')).toEqual({
      page: '1',
      pageSize: '25',
      sort: 'eventStartsAt',
      search: 'hack',
      categoryTypeId: 'cat-2',
      eventDateFrom: '2026-10-01',
      eventDateTo: '2026-10-31',
      signupFrom: '2026-09-01',
    })
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

  it('creates an event when none is edited and replaces the edited one', async () => {
    const calls: string[] = []
    server.use(
      http.get('/api/events', () => HttpResponse.json(paged([]))),
      http.get('/api/events/:id', () => HttpResponse.json(buildEventResponse({ title: 'Loaded' }))),
      http.post('/api/events', () => {
        calls.push('POST')
        return HttpResponse.json(buildEventResponse(), { status: 201 })
      }),
      http.put('/api/events/:id', ({ params }) => {
        calls.push(`PUT ${String(params.id)}`)
        return HttpResponse.json(buildEventResponse())
      }),
      http.delete('/api/events/:id', () => noContent()),
    )
    const { result } = await mountComposable(() => useEventsAdmin())

    result.dialog.openCreate()
    result.save(INPUT)
    await expectNotification(t('pages.admin.events.toasts.created'))
    expect(result.dialog.visible.value).toBe(false)

    await result.dialog.openEdit(buildEventListing())
    expect(result.dialog.editing.value?.title).toBe('Loaded')
    result.save(INPUT)
    await expectNotification(t('pages.admin.events.toasts.updated'))

    expect(calls).toEqual(['POST', 'PUT event-1'])
  })
})
