import { describe, expect, it } from 'vitest'

import {
  assignActivityRequest,
  assignHouseholdRequest,
  changeAssignmentRoleRequest,
  changeAssignmentStatusRequest,
  createActivityRequest,
  deleteActivityRequest,
  getActivitiesAdminPageRequest,
  getActivityByIdRequest,
  getEventActivitiesRequest,
  getEventActivityOptionsRequest,
  getHouseholdAssignmentsRequest,
  getHouseholdMembersRequest,
  getMyAssignmentsRequest,
  getSignupRolesRequest,
  unassignActivityRequest,
  updateActivityRequest,
  verifyOverlapsRequest,
} from '@/entities/activity'
import { ApiError } from '@/shared/api'
import type {
  ActivityResponse,
  AssignedActivityResponse,
  HouseholdMemberAssignmentResponse,
  HouseholdSignupRolesResponse,
  TimeOverlapResponse,
} from '@/shared/api/generated/models'

import { buildUserResponse } from '../../../../support/fixtures/user'
import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'

function queryOf(url: URL | undefined): Record<string, string> {
  return Object.fromEntries(url?.searchParams ?? [])
}

const noContent = () => new HttpResponse(null, { status: 204 })

describe('activity requests', () => {
  it('loads the event timeline ordered by start time and maps it', async () => {
    let url: URL | undefined
    const items: ActivityResponse[] = [{ id: 'activity-1', title: 'Robótica' }]
    server.use(
      http.get('/api/activities', ({ request }) => {
        url = new URL(request.url)
        return HttpResponse.json(paged(items))
      }),
    )

    const activities = await getEventActivitiesRequest('event-1')

    expect(activities.map((activity) => activity.title)).toEqual(['Robótica'])
    expect(queryOf(url)).toEqual({ eventId: 'event-1', pageSize: '100', sort: 'activityStartsAt' })
  })

  it('returns raw activities for admin selectors and tables', async () => {
    const urls: URL[] = []
    const items: ActivityResponse[] = [{ id: 'activity-1', modalityId: 'm1' }]
    server.use(
      http.get('/api/activities', ({ request }) => {
        urls.push(new URL(request.url))
        return HttpResponse.json(paged(items, 7))
      }),
    )

    await expect(getEventActivityOptionsRequest('event-1')).resolves.toEqual(items)
    await expect(getActivitiesAdminPageRequest({ page: 2, pageSize: 10 })).resolves.toEqual({
      items,
      total: 7,
    })
    expect(queryOf(urls[1])).toEqual({ page: '2', pageSize: '10' })
  })

  it('treats a page without items as empty', async () => {
    server.use(http.get('/api/activities', () => HttpResponse.json({})))

    await expect(getActivitiesAdminPageRequest({})).resolves.toEqual({ items: [], total: 0 })
  })

  it('filters own assignments by event only when an event id is given', async () => {
    const urls: URL[] = []
    const data: AssignedActivityResponse[] = [
      { activityId: 'activity-1', status: { name: 'Confirmada' }, roleType: { name: 'Mentor' } },
    ]
    server.use(
      http.get('/api/me/assigned-activities', ({ request }) => {
        urls.push(new URL(request.url))
        return HttpResponse.json(data)
      }),
    )

    await expect(getMyAssignmentsRequest('event-1')).resolves.toEqual([
      { activityId: 'activity-1', status: 'Confirmada', roleName: 'Mentor' },
    ])
    await getMyAssignmentsRequest()

    expect(queryOf(urls[0])).toEqual({ eventId: 'event-1' })
    expect(queryOf(urls[1])).toEqual({})
  })

  it('returns no own assignments when the body is empty', async () => {
    server.use(http.get('/api/me/assigned-activities', noContent))

    await expect(getMyAssignmentsRequest()).resolves.toEqual([])
  })

  describe('getActivityByIdRequest', () => {
    it('maps the admin detail', async () => {
      server.use(
        http.get('/api/activities/activity-1', () =>
          HttpResponse.json<ActivityResponse>({ id: 'activity-1', title: 'Robótica' }),
        ),
      )

      await expect(getActivityByIdRequest('activity-1')).resolves.toMatchObject({
        id: 'activity-1',
        title: 'Robótica',
      })
    })

    it('resolves null when the activity no longer exists', async () => {
      server.use(http.get('/api/activities/missing', () => apiError(404, 'ActivityNotFound')))

      await expect(getActivityByIdRequest('missing')).resolves.toBeNull()
    })

    it('rethrows server errors', async () => {
      server.use(http.get('/api/activities/broken', () => apiError(500)))

      await expect(getActivityByIdRequest('broken')).rejects.toBeInstanceOf(ApiError)
    })
  })

  it('maps the household assignments of an event', async () => {
    const data: HouseholdMemberAssignmentResponse[] = [
      { activityId: 'activity-1', userId: 'child-1', firstName: 'Byron', lastName: 'King' },
    ]
    server.use(
      http.get('/api/activities/household-assignments/event-1', () => HttpResponse.json(data)),
    )

    await expect(getHouseholdAssignmentsRequest('event-1')).resolves.toEqual([
      { activityId: 'activity-1', userId: 'child-1', name: 'Byron King', roleName: '', status: '' },
    ])

    server.use(http.get('/api/activities/household-assignments/event-2', noContent))
    await expect(getHouseholdAssignmentsRequest('event-2')).resolves.toEqual([])
  })

  it('lists household members as the minors of the user', async () => {
    let url: URL | undefined
    server.use(
      http.get('/api/users', ({ request }) => {
        url = new URL(request.url)
        return HttpResponse.json(paged([buildUserResponse({ id: 'child-1', firstName: 'Byron' })]))
      }),
    )

    await expect(getHouseholdMembersRequest('user-1')).resolves.toEqual([
      { id: 'child-1', name: 'Byron Lovelace' },
    ])
    expect(queryOf(url)).toEqual({ parentId: 'user-1', pageSize: '100', sort: 'firstName' })
  })

  it('maps signup roles per household member', async () => {
    const data: HouseholdSignupRolesResponse[] = [
      { userId: 'user-1', roles: [{ id: 'role-1', name: 'Mentor' }] },
    ]
    server.use(http.get('/api/activities/signup-roles', () => HttpResponse.json(data)))

    await expect(getSignupRolesRequest()).resolves.toEqual([
      { userId: 'user-1', roles: [{ id: 'role-1', name: 'Mentor' }] },
    ])

    server.use(http.get('/api/activities/signup-roles', noContent))
    await expect(getSignupRolesRequest()).resolves.toEqual([])
  })

  it('checks schedule overlaps for a user', async () => {
    const data: TimeOverlapResponse = {
      hasOverlaps: true,
      overlaps: [{ activityId: 'activity-2', title: 'Python' }],
    }
    server.use(
      http.get('/api/activities/activity-1/overlaps/user-1', () => HttpResponse.json(data)),
    )

    await expect(verifyOverlapsRequest('activity-1', 'user-1')).resolves.toEqual({
      hasOverlaps: true,
      overlaps: [{ activityId: 'activity-2', title: 'Python', startsAt: null, endsAt: null }],
    })
  })

  it('signs a user up without terms decisions by default and with them when given', async () => {
    const bodies: unknown[] = []
    server.use(
      http.patch('/api/activities/activity-1/user-1/assign', async ({ request }) => {
        bodies.push(await request.json())
        return noContent()
      }),
    )

    await assignActivityRequest('activity-1', 'user-1', 'role-1')
    await assignActivityRequest('activity-1', 'user-1', 'role-1', [
      { termsDocumentId: 'terms-1', accepted: true },
    ])

    expect(bodies).toEqual([
      { activityRoleTypeId: 'role-1' },
      {
        activityRoleTypeId: 'role-1',
        termsDecisions: [{ termsDocumentId: 'terms-1', accepted: true }],
      },
    ])
  })

  it('surfaces a missing terms acceptance as an ApiError code', async () => {
    server.use(
      http.patch('/api/activities/activity-1/user-1/assign', () =>
        apiError(409, 'EventTermsAcceptanceRequired'),
      ),
    )

    await expect(assignActivityRequest('activity-1', 'user-1', 'role-1')).rejects.toMatchObject({
      code: 'EventTermsAcceptanceRequired',
    })
  })

  it('signs several household members up in one request', async () => {
    const bodies: unknown[] = []
    server.use(
      http.post('/api/activities/activity-1/assign-household', async ({ request }) => {
        bodies.push(await request.json())
        return noContent()
      }),
    )

    const assignments = [
      { userId: 'user-1', roleId: 'role-mentor' },
      { userId: 'child-1', roleId: 'role-participant' },
    ]
    await assignHouseholdRequest('activity-1', assignments)
    await assignHouseholdRequest(
      'activity-1',
      [],
      [{ termsDocumentId: 'terms-1', accepted: false }],
    )

    expect(bodies).toEqual([
      {
        assignments: [
          { userId: 'user-1', activityRoleTypeId: 'role-mentor' },
          { userId: 'child-1', activityRoleTypeId: 'role-participant' },
        ],
      },
      {
        assignments: [],
        termsDecisions: [{ termsDocumentId: 'terms-1', accepted: false }],
      },
    ])
  })

  it('removes a signup', async () => {
    let called = false
    server.use(
      http.patch('/api/activities/activity-1/user-1/unassign', () => {
        called = true
        return noContent()
      }),
    )

    await expect(unassignActivityRequest('activity-1', 'user-1')).resolves.toBeUndefined()
    expect(called).toBe(true)
  })

  it('creates, updates and deletes activities through the admin endpoints', async () => {
    const calls: { method: string; path: string; body: unknown }[] = []
    const record = async ({ request }: { request: Request }) => {
      const body: unknown = request.method === 'DELETE' ? null : await request.json()
      calls.push({ method: request.method, path: new URL(request.url).pathname, body })
      return request.method === 'DELETE'
        ? noContent()
        : HttpResponse.json<ActivityResponse>({ id: 'activity-1', title: 'Saved' })
    }
    server.use(
      http.post('/api/activities/event-1', record),
      http.put('/api/activities/activity-1', record),
      http.delete('/api/activities/activity-1', record),
    )

    await expect(
      createActivityRequest('event-1', { title: 'Nueva', activityModalityTypeId: 'm1' }),
    ).resolves.toEqual({ id: 'activity-1', title: 'Saved' })
    await expect(updateActivityRequest('activity-1', { title: 'Editada' })).resolves.toEqual({
      id: 'activity-1',
      title: 'Saved',
    })
    const deleted = await deleteActivityRequest('activity-1')

    expect(deleted.status).toBe(204)
    expect(calls).toEqual([
      {
        method: 'POST',
        path: '/api/activities/event-1',
        body: { title: 'Nueva', activityModalityTypeId: 'm1' },
      },
      { method: 'PUT', path: '/api/activities/activity-1', body: { title: 'Editada' } },
      { method: 'DELETE', path: '/api/activities/activity-1', body: null },
    ])
  })

  it('changes the status and role of an assignment', async () => {
    const bodies: unknown[] = []
    server.use(
      http.patch('/api/activities/activity-1/user-1/change-status', async ({ request }) => {
        bodies.push(await request.json())
        return HttpResponse.json({ status: 'changed' })
      }),
      http.patch('/api/activities/activity-1/user-1/change-role', async ({ request }) => {
        bodies.push(await request.json())
        return HttpResponse.json({ role: 'changed' })
      }),
    )

    await expect(
      changeAssignmentStatusRequest('activity-1', 'user-1', { assignmentStatusId: 'status-ok' }),
    ).resolves.toEqual({ status: 'changed' })
    await expect(
      changeAssignmentRoleRequest('activity-1', 'user-1', { activityRoleTypeId: 'role-2' }),
    ).resolves.toEqual({ role: 'changed' })

    expect(bodies).toEqual([{ assignmentStatusId: 'status-ok' }, { activityRoleTypeId: 'role-2' }])
  })
})
