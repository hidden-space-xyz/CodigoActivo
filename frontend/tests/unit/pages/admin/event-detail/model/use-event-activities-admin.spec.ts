import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import type { ActivityInput } from '@/entities/activity'
import { eventReportKeys } from '@/pages/admin/event-detail/api/queries'
import { useEventActivitiesAdmin } from '@/pages/admin/event-detail/model/use-event-activities-admin'

import {
  apiError,
  http,
  HttpResponse,
  noContent,
  paged,
  server,
} from '../../../../../support/server'
import { buildActivityResponse } from '../../../../../support/builders'
import { expectNotification, queryOf } from '../../../../../support/dom'
import { buildActivityListing } from '../../../../../support/models'
import { mountComposable, t } from '../../../../../support/render'

const INPUT: ActivityInput = {
  title: 'Robotics',
  description: 'Build robots',
  location: 'Room 1',
  modalityId: 'mod-1',
  startsAt: '2026-10-10T09:00:00.000Z',
  endsAt: '2026-10-10T11:00:00.000Z',
  thumbnailId: 'thumb',
  roleCapacities: [],
}

describe('useEventActivitiesAdmin', () => {
  it('lists the activities of the event by start time, filtered by modality', async () => {
    const urls: string[] = []
    server.use(
      http.get('/api/activities', ({ request }) => {
        urls.push(request.url)
        return HttpResponse.json(paged([buildActivityResponse()]))
      }),
    )

    const { result } = await mountComposable(() => useEventActivitiesAdmin(() => 'event-1'))
    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))
    expect(queryOf(urls[0] ?? '')).toEqual({
      eventId: 'event-1',
      page: '1',
      pageSize: '25',
      sort: 'activityStartsAt',
    })

    result.modalityId.value = 'mod-2'
    await vi.waitFor(() => expect(queryOf(urls.at(-1) ?? '').modalityTypeId).toBe('mod-2'))
  })

  it('creates, edits and deletes activities, refreshing the counts and attendees', async () => {
    const calls: string[] = []
    server.use(
      http.get('/api/activities', () => HttpResponse.json(paged([]))),
      http.get('/api/activities/:id', () =>
        HttpResponse.json(buildActivityResponse({ title: 'Loaded' })),
      ),
      http.post('/api/activities/:eventId', ({ params }) => {
        calls.push(`POST ${String(params.eventId)}`)
        return HttpResponse.json(buildActivityResponse(), { status: 201 })
      }),
      http.put('/api/activities/:id', ({ params }) => {
        calls.push(`PUT ${String(params.id)}`)
        return HttpResponse.json(buildActivityResponse())
      }),
      http.delete('/api/activities/:id', ({ params }) => {
        calls.push(`DELETE ${String(params.id)}`)
        return noContent()
      }),
    )
    const { result, queryClient } = await mountComposable(() =>
      useEventActivitiesAdmin(() => 'event-1'),
    )
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    result.dialog.openCreate()
    result.save(INPUT)
    await expectNotification(t('pages.admin.eventDetail.toast.activityCreated'))
    expect(result.dialog.visible.value).toBe(false)

    await result.dialog.openEdit(buildActivityListing())
    expect(result.dialog.editing.value?.title).toBe('Loaded')
    result.save(INPUT)
    await expectNotification(t('pages.admin.eventDetail.toast.activityUpdated'))

    expect(calls).toEqual(['POST event-1', 'PUT act-1'])
    expect(invalidate).toHaveBeenCalledWith({ queryKey: eventReportKeys.stats('event-1') })
    expect(invalidate).toHaveBeenCalledWith({ queryKey: eventReportKeys.attendees() })
  })

  it('tells the admin when the edited activity no longer exists', async () => {
    server.use(
      http.get('/api/activities', () => HttpResponse.json(paged([]))),
      http.get('/api/activities/:id', () => apiError(404, 'ActivityNotFound')),
    )
    const { result } = await mountComposable(() => useEventActivitiesAdmin(() => 'event-1'))

    await result.dialog.openEdit(buildActivityListing())
    await flushPromises()

    await expectNotification(t('pages.admin.eventDetail.toast.activityNotFound'))
    expect(result.dialog.visible.value).toBe(false)
  })
})
