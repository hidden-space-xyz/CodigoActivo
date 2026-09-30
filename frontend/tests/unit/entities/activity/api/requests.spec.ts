import { describe, expect, it } from 'vitest'

import type { ActivityInput } from '@/entities/activity'
import {
  assignActivityRequest,
  assignHouseholdRequest,
  changeAssignmentRoleRequest,
  changeAssignmentStatusRequest,
  createActivityRequest,
  deleteActivityRequest,
  getActivitiesPageRequest,
  getActivityModalitiesRequest,
  getActivityRequest,
  getActivityRolesRequest,
  getAssignmentStatusesRequest,
  getEventActivitiesRequest,
  getHouseholdAssignmentsRequest,
  getHouseholdMembersRequest,
  getMyAssignmentsRequest,
  getSignupRolesRequest,
  unassignActivityRequest,
  updateActivityRequest,
  verifyOverlapsRequest,
} from '@/entities/activity/api/requests'
import { ApiError } from '@/shared/api'
import type {
  ActivityResponse,
  HouseholdSignupRolesResponse,
  TimeOverlapResponse,
} from '@/shared/api/generated/models'

import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'
import {
  buildActivityResponse,
  buildAssignedActivity,
  buildHouseholdAssignment,
  buildUserResponse,
  assignmentStatusTypes,
  modalityTypes,
  roleTypes,
} from '../../../../support/builders'

function queryOf(url: URL | undefined): Record<string, string> {
  return Object.fromEntries(url?.searchParams ?? [])
}

const noContent = () => new HttpResponse(null, { status: 204 })

describe('activity requests', () => {
  it('loads the event timeline ordered by start time and maps it', async () => {
    let url: URL | undefined
    const items: ActivityResponse[] = [
      buildActivityResponse({ id: 'activity-1', title: 'Robótica' }),
    ]
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

  it('pages the admin table of an event, mapping each activity to a row', async () => {
    let url: URL | undefined
    server.use(
      http.get('/api/activities', ({ request }) => {
        url = new URL(request.url)
        return HttpResponse.json(paged([buildActivityResponse({ id: 'activity-1' })], 7))
      }),
    )

    await expect(
      getActivitiesPageRequest({ eventId: 'event-1', page: 2, pageSize: 10 }),
    ).resolves.toEqual({
      items: [
        {
          id: 'activity-1',
          title: 'Robotics',
          location: 'Room 1',
          modality: 'On site',
          startsAt: '2026-10-10T09:00:00.000Z',
          endsAt: '2026-10-10T11:00:00.000Z',
          thumbnailId: 'thumb-event',
        },
      ],
      total: 7,
    })
    expect(queryOf(url)).toEqual({ eventId: 'event-1', page: '2', pageSize: '10' })
  })

  it('loads the own signups to the activities of an event', async () => {
    let url: URL | undefined
    server.use(
      http.get('/api/me/assigned-activities', ({ request }) => {
        url = new URL(request.url)
        return HttpResponse.json([
          buildAssignedActivity({
            activityId: 'activity-1',
            status: { id: 'status-2', name: 'Confirmada' },
            roleType: { id: 'role-mentor', name: 'Mentor' },
          }),
        ])
      }),
    )

    await expect(getMyAssignmentsRequest('event-1')).resolves.toEqual([
      { activityId: 'activity-1', status: 'Confirmada', roleName: 'Mentor' },
    ])
    expect(queryOf(url)).toEqual({ eventId: 'event-1' })
  })

  describe('getActivityRequest', () => {
    it('maps the admin detail', async () => {
      server.use(
        http.get('/api/activities/activity-1', () =>
          HttpResponse.json(buildActivityResponse({ id: 'activity-1', title: 'Robótica' })),
        ),
      )

      await expect(getActivityRequest('activity-1')).resolves.toMatchObject({
        id: 'activity-1',
        title: 'Robótica',
      })
    })

    it('resolves null when the activity no longer exists', async () => {
      server.use(http.get('/api/activities/missing', () => apiError(404, 'ActivityNotFound')))

      await expect(getActivityRequest('missing')).resolves.toBeNull()
    })

    it('rethrows server errors', async () => {
      server.use(http.get('/api/activities/broken', () => apiError(500)))

      await expect(getActivityRequest('broken')).rejects.toBeInstanceOf(ApiError)
    })
  })

  it('maps the household signups of an event', async () => {
    server.use(
      http.get('/api/activities/household-assignments/event-1', () =>
        HttpResponse.json([buildHouseholdAssignment({ activityId: 'activity-1' })]),
      ),
    )

    await expect(getHouseholdAssignmentsRequest('event-1')).resolves.toEqual([
      {
        activityId: 'activity-1',
        userId: 'child-1',
        name: 'Byron King',
        roleName: 'Participante',
        status: 'Solicitada',
      },
    ])
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
  })

  it('checks schedule overlaps for a user', async () => {
    const data: TimeOverlapResponse = {
      hasOverlaps: true,
      overlaps: [
        {
          activityId: 'activity-2',
          title: 'Python',
          startsAt: '2026-10-01T10:00:00Z',
          endsAt: '2026-10-01T12:00:00Z',
        },
      ],
    }
    server.use(
      http.get('/api/activities/activity-1/overlaps/user-1', () => HttpResponse.json(data)),
    )

    await expect(verifyOverlapsRequest('activity-1', 'user-1')).resolves.toEqual(data)
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
    const input: ActivityInput = {
      title: 'Nueva',
      description: 'Texto',
      location: 'Aula 1',
      modalityId: 'm1',
      startsAt: '2026-10-10T09:00:00.000Z',
      endsAt: '2026-10-10T11:00:00.000Z',
      thumbnailId: 't1',
      roleCapacities: [{ roleTypeId: 'role-1', desiredCount: 5 }],
    }
    const body = {
      title: 'Nueva',
      description: 'Texto',
      location: 'Aula 1',
      activityModalityTypeId: 'm1',
      activityStartsAt: '2026-10-10T09:00:00.000Z',
      activityEndsAt: '2026-10-10T11:00:00.000Z',
      thumbnailId: 't1',
      roleCapacities: [{ activityRoleTypeId: 'role-1', desiredCount: 5 }],
    }
    const calls: { method: string; path: string; body: unknown }[] = []
    const record = async ({ request }: { request: Request }) => {
      const sent: unknown = request.method === 'DELETE' ? null : await request.json()
      calls.push({ method: request.method, path: new URL(request.url).pathname, body: sent })
      return request.method === 'DELETE' ? noContent() : HttpResponse.json(buildActivityResponse())
    }
    server.use(
      http.post('/api/activities/event-1', record),
      http.put('/api/activities/activity-1', record),
      http.delete('/api/activities/activity-1', record),
    )

    await createActivityRequest('event-1', input)
    await updateActivityRequest('activity-1', { ...input, roleCapacities: [] })
    await deleteActivityRequest('activity-1')

    expect(calls).toEqual([
      { method: 'POST', path: '/api/activities/event-1', body },
      {
        method: 'PUT',
        path: '/api/activities/activity-1',
        body: { ...body, roleCapacities: null },
      },
      { method: 'DELETE', path: '/api/activities/activity-1', body: null },
    ])
  })

  it('changes the status and role of an assignment', async () => {
    const bodies: unknown[] = []
    server.use(
      http.patch('/api/activities/activity-1/user-1/change-status', async ({ request }) => {
        bodies.push(await request.json())
        return HttpResponse.json({ id: 'status-ok', name: 'Confirmed' })
      }),
      http.patch('/api/activities/activity-1/user-1/change-role', async ({ request }) => {
        bodies.push(await request.json())
        return HttpResponse.json(buildActivityResponse())
      }),
    )

    await changeAssignmentStatusRequest('activity-1', 'user-1', 'status-ok')
    await changeAssignmentRoleRequest('activity-1', 'user-1', 'role-2')

    expect(bodies).toEqual([{ assignmentStatusId: 'status-ok' }, { activityRoleTypeId: 'role-2' }])
  })

  it('lists the roles, signup statuses and modalities of activities', async () => {
    server.use(
      http.get('/api/activities/roleType', () => HttpResponse.json(roleTypes)),
      http.get('/api/activities/assignment-status-types', () =>
        HttpResponse.json(assignmentStatusTypes),
      ),
      http.get('/api/activities/modality-types', () => HttpResponse.json(modalityTypes)),
    )

    await expect(getActivityRolesRequest()).resolves.toEqual([
      { id: 'role-1', name: 'Volunteer' },
      { id: 'role-2', name: 'Mentor' },
    ])
    await expect(getAssignmentStatusesRequest()).resolves.toEqual([
      { id: 'status-1', name: 'Requested', color: '#aabbcc' },
      { id: 'status-2', name: 'Confirmed', color: '#00ff00' },
    ])
    await expect(getActivityModalitiesRequest()).resolves.toEqual([
      { id: 'mod-1', name: 'On site' },
      { id: 'mod-2', name: 'Online' },
    ])
  })
})
