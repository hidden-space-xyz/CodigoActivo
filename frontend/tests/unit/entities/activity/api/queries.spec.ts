import { describe, expect, it } from 'vitest'

import {
  activityCatalogKeys,
  activityKeys,
  activityList,
  activityQueries,
} from '@/entities/activity'

import { apiError, http, HttpResponse, paged, server } from '../../../../support/server'
import {
  assignmentStatusTypes,
  buildActivityResponse,
  modalityTypes,
  roleTypes,
  buildAssignedActivity,
  buildHouseholdAssignment,
  buildUserResponse,
} from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

describe('activityQueries', () => {
  it('loads the activities of an event and the signups of the user and their household', async () => {
    server.use(
      http.get('/api/activities', () =>
        HttpResponse.json(paged([buildActivityResponse({ id: 'activity-1' })])),
      ),
      http.get('/api/me/assigned-activities', () =>
        HttpResponse.json([buildAssignedActivity({ activityId: 'activity-1' })]),
      ),
      http.get('/api/activities/household-assignments/:eventId', () =>
        HttpResponse.json([buildHouseholdAssignment({ activityId: 'activity-1' })]),
      ),
    )
    const client = createTestQueryClient()

    await expect(client.fetchQuery(activityQueries.ofEvent('event-1'))).resolves.toEqual([
      expect.objectContaining({ id: 'activity-1' }),
    ])
    await expect(client.fetchQuery(activityQueries.myAssignments('event-1'))).resolves.toEqual([
      expect.objectContaining({ activityId: 'activity-1' }),
    ])
    await expect(
      client.fetchQuery(activityQueries.householdAssignments('event-1')),
    ).resolves.toEqual([expect.objectContaining({ userId: 'child-1' })])
    expect(activityQueries.ofEvent('event-1').queryKey).toEqual(activityKeys.ofEvent('event-1'))
    expect(activityQueries.myAssignments('event-1').queryKey).toEqual(
      activityKeys.myAssignments('event-1'),
    )
    expect(activityQueries.householdAssignments('event-1').queryKey).toEqual(
      activityKeys.householdAssignments('event-1'),
    )
  })

  it('keys the household members by user under the household members root', async () => {
    server.use(
      http.get('/api/users', () =>
        HttpResponse.json(paged([buildUserResponse({ id: 'child-1', firstName: 'Byron' })])),
      ),
    )
    const client = createTestQueryClient()

    await expect(client.fetchQuery(activityQueries.householdMembers('user-1'))).resolves.toEqual([
      { id: 'child-1', name: 'Byron Lovelace' },
    ])
    expect(activityQueries.householdMembers('user-1').queryKey).toEqual([
      ...activityKeys.householdMembers(),
      'user-1',
    ])
  })

  it('loads the signup roles and asks for overlaps afresh every time', async () => {
    let overlapChecks = 0
    server.use(
      http.get('/api/activities/signup-roles', () =>
        HttpResponse.json([{ userId: 'user-1', roles: [{ id: 'role-1', name: 'Mentor' }] }]),
      ),
      http.get('/api/activities/:activityId/overlaps/:userId', () => {
        overlapChecks += 1
        return HttpResponse.json({ hasOverlaps: false, overlaps: [] })
      }),
    )
    const client = createTestQueryClient()

    await expect(client.fetchQuery(activityQueries.signupRoles())).resolves.toEqual([
      { userId: 'user-1', roles: [{ id: 'role-1', name: 'Mentor' }] },
    ])
    await client.fetchQuery(activityQueries.overlaps('activity-1', 'user-1'))
    await client.fetchQuery(activityQueries.overlaps('activity-1', 'user-1'))

    expect(overlapChecks).toBe(2)
  })
})

describe('activityKeys', () => {
  it('nests activities under one root and keeps the catalogs apart', () => {
    expect(activityKeys.all).toEqual(['activities'])
    expect(activityKeys.ofEvent('e1')).toEqual(['activities', 'of-event', 'e1'])
    expect(activityKeys.detail('a1')).toEqual(['activities', 'detail', 'a1'])
    expect(activityKeys.list()).toEqual(['activities', 'list'])
    expect(activityKeys.overlaps('a1', 'u1')).toEqual(['activities', 'overlaps', 'a1', 'u1'])
    expect(activityCatalogKeys.roles()).toEqual(['activity-catalogs', 'roles'])
    expect(activityCatalogKeys.statuses()).toEqual(['activity-catalogs', 'statuses'])
    expect(activityCatalogKeys.modalities()).toEqual(['activity-catalogs', 'modalities'])
  })
})

describe('admin activity queries', () => {
  it('loads an activity to edit, or null when it no longer exists', async () => {
    server.use(
      http.get('/api/activities/act-1', () => HttpResponse.json(buildActivityResponse())),
      http.get('/api/activities/missing', () => apiError(404)),
    )
    const client = createTestQueryClient()

    await expect(client.fetchQuery(activityQueries.detail('act-1'))).resolves.toMatchObject({
      id: 'act-1',
      modalityId: 'mod-1',
    })
    await expect(client.fetchQuery(activityQueries.detail('missing'))).resolves.toBeNull()
  })

  it('loads the catalogs of roles, statuses and modalities', async () => {
    server.use(
      http.get('/api/activities/roleType', () => HttpResponse.json(roleTypes)),
      http.get('/api/activities/assignment-status-types', () =>
        HttpResponse.json(assignmentStatusTypes),
      ),
      http.get('/api/activities/modality-types', () => HttpResponse.json(modalityTypes)),
    )
    const client = createTestQueryClient()

    await expect(client.fetchQuery(activityQueries.roles())).resolves.toHaveLength(2)
    await expect(client.fetchQuery(activityQueries.statuses())).resolves.toHaveLength(2)
    await expect(client.fetchQuery(activityQueries.modalities())).resolves.toHaveLength(2)
  })

  it('pages the admin table under the list key', async () => {
    server.use(
      http.get('/api/activities', () => HttpResponse.json(paged([buildActivityResponse()], 1))),
    )

    await expect(activityList.fetchPage({ eventId: 'event-1' })).resolves.toMatchObject({
      total: 1,
      items: [{ id: 'act-1', modality: 'On site' }],
    })
    expect(activityList.queryKey).toEqual(activityKeys.list())
  })
})
