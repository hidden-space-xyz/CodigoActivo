import { ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { toEventAttendee } from '@/pages/admin/event-detail/api/mapper'
import { useEventAttendees } from '@/pages/admin/event-detail/model/use-event-attendees'

import { http, HttpResponse, paged, server } from '../../../../../support/server'
import { useCatalogHandlers } from '../../../../../support/api/catalogs'
import {
  buildActivityResponse,
  buildAttendee,
  buildAttendeeAssignment,
} from '../../../../../support/builders'
import { queryOf } from '../../../../../support/dom'
import { mountComposable } from '../../../../../support/render'

function serveAttendees(urls: string[] = []) {
  useCatalogHandlers()
  server.use(
    http.get('/api/activities', () => HttpResponse.json(paged([buildActivityResponse()]))),
    http.get('/api/reports/events/:eventId/attendees', ({ request }) => {
      urls.push(request.url)
      return HttpResponse.json(paged([buildAttendee()], 1))
    }),
  )
  return urls
}

describe('useEventAttendees', () => {
  it('only loads the attendees while the tab is active', async () => {
    const urls = serveAttendees()
    const active = ref(false)

    const { result } = await mountComposable(() =>
      useEventAttendees(
        () => 'event-1',
        () => active.value,
      ),
    )
    await flushPromises()
    expect(urls).toEqual([])

    active.value = true
    await vi.waitFor(() => expect(result.table.items.value).toHaveLength(1))
    expect(queryOf(urls[0] ?? '')).toEqual({ page: '1', pageSize: '25', sort: 'firstName' })
  })

  it('sends the debounced search and the filters, ignoring cleared ones', async () => {
    const urls = serveAttendees()
    const { result } = await mountComposable(() =>
      useEventAttendees(
        () => 'event-1',
        () => true,
      ),
    )
    await vi.waitFor(() => expect(urls).toHaveLength(1))
    expect(result.hasActiveFilters.value).toBe(false)

    result.searchText.value = '  ada '
    result.genderFilter.value = 'Female'
    result.roleFilter.value = 'role-1'
    result.statusFilter.value = ''
    expect(result.hasActiveFilters.value).toBe(true)

    await vi.waitFor(() => expect(queryOf(urls.at(-1) ?? '').search).toBe('ada'))
    expect(queryOf(urls.at(-1) ?? '')).toMatchObject({ gender: 'Female', roleTypeId: 'role-1' })
    expect(result.filters()).toEqual({ search: 'ada', gender: 'Female', roleTypeId: 'role-1' })
    expect(result.statusFilter.value).toBeNull()
  })

  it('changes the sort field and direction from the first page', async () => {
    const urls = serveAttendees()
    const { result } = await mountComposable(() =>
      useEventAttendees(
        () => 'event-1',
        () => true,
      ),
    )
    await vi.waitFor(() => expect(urls).toHaveLength(1))

    result.table.first.value = 25
    result.sort.value = 'type,firstName'
    expect(result.table.first.value).toBe(0)
    await vi.waitFor(() => expect(queryOf(urls.at(-1) ?? '').sort).toBe('type,firstName'))

    expect(result.ascending.value).toBe(true)
    result.toggleSortDirection()
    expect(result.ascending.value).toBe(false)
    await vi.waitFor(() => expect(queryOf(urls.at(-1) ?? '').sort).toBe('-type,firstName'))
  })

  it('colors each signup with the color of its status', async () => {
    serveAttendees()
    const { result } = await mountComposable(() =>
      useEventAttendees(
        () => 'event-1',
        () => true,
      ),
    )
    await vi.waitFor(() => expect(result.statuses.data.value).toHaveLength(2))

    expect(result.statusColor(toAssignment('status-2'))).toBe('#00ff00')
    expect(result.statusColor(toAssignment('status-9'))).toBeNull()
  })
})

function toAssignment(statusId: string) {
  const attendee = toEventAttendee(
    buildAttendee({ assignments: [buildAttendeeAssignment({ statusId })] }),
  )
  const [assignment] = attendee.assignments
  if (!assignment) throw new Error('Assignment not mapped')
  return assignment
}
