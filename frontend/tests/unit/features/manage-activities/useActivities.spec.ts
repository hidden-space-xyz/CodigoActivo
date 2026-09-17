import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { useActivities } from '@/features/manage-activities'
import type { CreateActivityRequest } from '@/shared/api/generated/models'

import {
  buildActivity,
  EVENT_ID,
  setupComposable,
  THUMBNAIL_ID,
} from '../../../support/fixtures/admin-events/builders'
import { apiError, http, HttpResponse, paged, server } from '../../../support/server'

const body: CreateActivityRequest = {
  title: 'Robotics',
  description: 'Build robots',
  location: 'Room 1',
  activityModalityTypeId: 'mod-1',
  activityStartsAt: '2026-10-10T09:00:00.000Z',
  activityEndsAt: '2026-10-10T11:00:00.000Z',
  thumbnailId: THUMBNAIL_ID,
  roleCapacities: null,
}

describe('useActivities', () => {
  it('loads the event activities table and options, filtering by modality', async () => {
    const tableUrls: URL[] = []
    const optionUrls: URL[] = []
    server.use(
      http.get('/api/activities', ({ request }) => {
        const url = new URL(request.url)
        if (url.searchParams.get('pageSize') === '100') optionUrls.push(url)
        else tableUrls.push(url)
        return HttpResponse.json(paged([buildActivity()], 1))
      }),
    )

    const { result } = await setupComposable(() => useActivities(EVENT_ID))

    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))
    await vi.waitFor(() => expect(result.options.data.value).toHaveLength(1))
    expect(tableUrls[0]?.searchParams.get('eventId')).toBe(EVENT_ID)
    expect(tableUrls[0]?.searchParams.get('sort')).toBe('activityStartsAt')
    expect(tableUrls[0]?.searchParams.has('modalityTypeId')).toBe(false)
    expect(optionUrls[0]?.searchParams.get('eventId')).toBe(EVENT_ID)

    result.modalityTypeId.value = 'mod-2'
    result.table.columnFilter('title').value = 'robot'
    result.table.columnFilter('activityDate').value = ['2026-10-10', '2026-10-11']
    await flushPromises()

    await vi.waitFor(() =>
      expect(tableUrls.at(-1)?.searchParams.get('modalityTypeId')).toBe('mod-2'),
    )
    const last = tableUrls.at(-1)
    expect(last?.searchParams.get('title')).toBe('robot')
    expect(last?.searchParams.get('activityDateFrom')).toBe('2026-10-10')
    expect(last?.searchParams.get('activityDateTo')).toBe('2026-10-11')
  })

  it('creates, updates and deletes activities and refreshes activity and report queries', async () => {
    const calls: { method: string; path: string; body: unknown }[] = []
    const record = async (request: Request) => {
      const text = await request.text()
      calls.push({
        method: request.method,
        path: new URL(request.url).pathname,
        body: text ? JSON.parse(text) : null,
      })
    }
    server.use(
      http.get('/api/activities', () => HttpResponse.json(paged([]))),
      http.post('/api/activities/:eventId', async ({ request }) => {
        await record(request)
        return HttpResponse.json(buildActivity(), { status: 201 })
      }),
      http.put('/api/activities/:activityId', async ({ request }) => {
        await record(request)
        return HttpResponse.json(buildActivity())
      }),
      http.delete('/api/activities/:activityId', async ({ request }) => {
        await record(request)
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { result, queryClient } = await setupComposable(() => useActivities(EVENT_ID))
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    await result.create.mutateAsync(body)
    await result.update.mutateAsync({ id: 'act-1', body })
    await result.remove.mutateAsync('act-1')

    expect(calls).toEqual([
      { method: 'POST', path: `/api/activities/${EVENT_ID}`, body },
      { method: 'PUT', path: '/api/activities/act-1', body },
      { method: 'DELETE', path: '/api/activities/act-1', body: null },
    ])
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['activities'] })
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['reports', 'event-summary', EVENT_ID] })
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['reports', 'event-attendees'] })
    expect(invalidate).toHaveBeenCalledTimes(9)
  })

  it('fetches one activity as an edit model and null when missing', async () => {
    server.use(
      http.get('/api/activities', () => HttpResponse.json(paged([]))),
      http.get('/api/activities/:activityId', ({ params }) =>
        params.activityId === 'gone' ? apiError(404) : HttpResponse.json(buildActivity()),
      ),
    )

    const { result } = await setupComposable(() => useActivities(EVENT_ID))

    await expect(result.fetchOne('act-1')).resolves.toMatchObject({
      id: 'act-1',
      modalityId: 'mod-1',
      roleCapacities: [{ roleTypeId: 'role-1', desiredCount: 4 }],
    })
    await expect(result.fetchOne('gone')).resolves.toBeNull()
  })
})
