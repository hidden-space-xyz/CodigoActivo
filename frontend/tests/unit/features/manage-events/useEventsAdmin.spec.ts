import { ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useEvent, useEventsAdmin } from '@/features/manage-events'
import type { CreateEventRequest } from '@/shared/api/generated/models'

import {
  buildEvent,
  buildEventListItem,
  EVENT_ID,
  setupComposable,
  THUMBNAIL_ID,
} from '../../../support/fixtures/admin-events/builders'
import { apiError, http, HttpResponse, paged, server } from '../../../support/server'

const body: CreateEventRequest = {
  title: 'New',
  subtitle: 'Sub',
  description: '{}',
  categoryTypeIds: ['cat-1'],
  eventStartsAt: '2026-10-10',
  eventEndsAt: '2026-10-11',
  earlySignupStartsAt: null,
  signupStartsAt: '2026-09-01T00:00:00.000Z',
  signupEndsAt: '2026-09-30T00:00:00.000Z',
  thumbnailId: THUMBNAIL_ID,
  termsDocuments: null,
}

describe('useEventsAdmin', () => {
  it('loads the first page sorted by start date and sends column filters as query params', async () => {
    const urls: URL[] = []
    server.use(
      http.get('/api/events', ({ request }) => {
        urls.push(new URL(request.url))
        return HttpResponse.json(paged([buildEventListItem()], 1))
      }),
    )

    const { result } = await setupComposable(() => useEventsAdmin())
    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))

    const first = urls[0]
    expect(first?.searchParams.get('page')).toBe('1')
    expect(first?.searchParams.get('pageSize')).toBe('25')
    expect(first?.searchParams.get('sort')).toBe('eventStartsAt')

    result.table.columnFilter('title').value = 'hack'
    result.table.columnFilter('category').value = 'cat-2'
    result.table.columnFilter('eventDate').value = ['2026-10-01', '2026-10-31']
    result.table.columnFilter('signup').value = ['2026-09-01', null]
    await flushPromises()

    await vi.waitFor(() => expect(urls.length).toBeGreaterThan(1))
    const last = urls.at(-1)
    expect(last?.searchParams.get('title')).toBe('hack')
    expect(last?.searchParams.get('categoryTypeId')).toBe('cat-2')
    expect(last?.searchParams.get('eventDateFrom')).toBe('2026-10-01')
    expect(last?.searchParams.get('eventDateTo')).toBe('2026-10-31')
    expect(last?.searchParams.get('signupFrom')).toBe('2026-09-01')
    expect(last?.searchParams.has('signupTo')).toBe(false)
  })

  it('creates, updates, features and deletes events, invalidating event queries', async () => {
    const calls: { method: string; path: string; body: unknown }[] = []
    const record = async (request: Request) => {
      const url = new URL(request.url)
      const text = await request.text()
      calls.push({
        method: request.method,
        path: url.pathname,
        body: text ? JSON.parse(text) : null,
      })
    }
    server.use(
      http.get('/api/events', () => HttpResponse.json(paged([]))),
      http.post('/api/events', async ({ request }) => {
        await record(request)
        return HttpResponse.json(buildEvent(), { status: 201 })
      }),
      http.put('/api/events/:eventId', async ({ request }) => {
        await record(request)
        return HttpResponse.json(buildEvent())
      }),
      http.patch('/api/events/:eventId/feature', async ({ request }) => {
        await record(request)
        return HttpResponse.json(buildEvent({ featured: true }))
      }),
      http.delete('/api/events/:eventId', async ({ request }) => {
        await record(request)
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { result, queryClient } = await setupComposable(() => useEventsAdmin())
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    await result.create.mutateAsync(body)
    await result.update.mutateAsync({ id: EVENT_ID, body })
    await result.feature.mutateAsync(EVENT_ID)
    await result.remove.mutateAsync(EVENT_ID)

    expect(calls).toEqual([
      { method: 'POST', path: '/api/events', body },
      { method: 'PUT', path: `/api/events/${EVENT_ID}`, body },
      { method: 'PATCH', path: `/api/events/${EVENT_ID}/feature`, body: null },
      { method: 'DELETE', path: `/api/events/${EVENT_ID}`, body: null },
    ])
    expect(invalidate).toHaveBeenCalledTimes(4)
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['events'] })
  })

  it('fetches one event for editing and resolves null when it no longer exists', async () => {
    server.use(
      http.get('/api/events', () => HttpResponse.json(paged([]))),
      http.get('/api/events/:eventId', ({ params }) =>
        params.eventId === 'missing' ? apiError(404) : HttpResponse.json(buildEvent()),
      ),
    )

    const { result } = await setupComposable(() => useEventsAdmin())

    await expect(result.fetchOne(EVENT_ID)).resolves.toMatchObject({ id: EVENT_ID })
    await expect(result.fetchOne('missing')).resolves.toBeNull()
  })
})

describe('useEvent', () => {
  it('loads the admin detail and refetches when the id changes', async () => {
    const requested: string[] = []
    server.use(
      http.get('/api/events/:eventId', ({ params }) => {
        const id = String(params.eventId)
        requested.push(id)
        return HttpResponse.json(buildEvent({ id, title: `Event ${id}` }))
      }),
    )
    const eventId = ref('a')

    const { result } = await setupComposable(() => useEvent(eventId))
    await vi.waitFor(() => expect(result.data.value?.title).toBe('Event a'))

    eventId.value = 'b'
    await vi.waitFor(() => expect(result.data.value?.title).toBe('Event b'))
    expect(requested).toEqual(['a', 'b'])
  })
})
