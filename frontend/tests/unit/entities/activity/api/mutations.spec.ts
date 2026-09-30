import { MutationObserver } from '@tanstack/vue-query'
import { describe, expect, it } from 'vitest'

import {
  activityCatalogKeys,
  activityKeys,
  activityMutations,
  type ActivityInput,
} from '@/entities/activity'

import { apiError, http, HttpResponse, noContent, server } from '../../../../support/server'
import { buildActivityResponse } from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

type TestQueryClient = ReturnType<typeof createTestQueryClient>

function cachedSignups() {
  const client = createTestQueryClient()
  const keys = [
    activityKeys.ofEvent('event-1'),
    activityKeys.myAssignments('event-1'),
    activityKeys.householdAssignments('event-1'),
    activityKeys.ofEvent('event-2'),
  ]
  for (const queryKey of keys) client.setQueryData(queryKey, [])
  const invalidated = () =>
    keys.map((queryKey) => client.getQueryState(queryKey)?.isInvalidated ?? false)
  return { client, invalidated }
}

describe('activityMutations', () => {
  it('signs up and withdraws, then refreshes the activities and signups of the event', async () => {
    const calls: { path: string; body: unknown }[] = []
    server.use(
      http.patch('/api/activities/:activityId/:userId/assign', async ({ request, params }) => {
        calls.push({
          path: `${String(params.activityId)}/${String(params.userId)}/assign`,
          body: await request.json(),
        })
        return noContent()
      }),
      http.post('/api/activities/:activityId/assign-household', async ({ request, params }) => {
        calls.push({ path: `${String(params.activityId)}/household`, body: await request.json() })
        return noContent()
      }),
      http.patch('/api/activities/:activityId/:userId/unassign', ({ params }) => {
        calls.push({
          path: `${String(params.activityId)}/${String(params.userId)}/unassign`,
          body: null,
        })
        return noContent()
      }),
    )
    const runs = [
      (client: TestQueryClient) =>
        new MutationObserver(client, activityMutations.signUp('event-1', 'user-1')).mutate({
          activityId: 'activity-1',
          roleId: 'role-1',
          termsDecisions: [{ termsDocumentId: 'terms-1', accepted: true }],
        }),
      (client: TestQueryClient) =>
        new MutationObserver(client, activityMutations.signUpHousehold('event-1')).mutate({
          activityId: 'activity-1',
          assignments: [{ userId: 'child-1', roleId: 'role-2' }],
        }),
      (client: TestQueryClient) =>
        new MutationObserver(client, activityMutations.withdraw('event-1')).mutate({
          activityId: 'activity-1',
          userId: 'child-1',
        }),
    ]

    for (const run of runs) {
      const { client, invalidated } = cachedSignups()
      await run(client)
      expect(invalidated()).toEqual([true, true, true, false])
    }
    expect(calls).toEqual([
      {
        path: 'activity-1/user-1/assign',
        body: {
          activityRoleTypeId: 'role-1',
          termsDecisions: [{ termsDocumentId: 'terms-1', accepted: true }],
        },
      },
      {
        path: 'activity-1/household',
        body: { assignments: [{ userId: 'child-1', activityRoleTypeId: 'role-2' }] },
      },
      { path: 'activity-1/child-1/unassign', body: null },
    ])
  })

  it('leaves the signups alone when the API refuses the signup', async () => {
    server.use(
      http.patch('/api/activities/:activityId/:userId/assign', () =>
        apiError(409, 'ActivityAssignmentAlreadyExists'),
      ),
    )
    const { client, invalidated } = cachedSignups()

    await expect(
      new MutationObserver(client, activityMutations.signUp('event-1', 'user-1')).mutate({
        activityId: 'activity-1',
        roleId: 'role-1',
      }),
    ).rejects.toMatchObject({ status: 409 })

    expect(invalidated()).toEqual([false, false, false, false])
  })
})

describe('admin activity mutations', () => {
  const input: ActivityInput = {
    title: 'Robotics',
    description: 'Build robots',
    location: 'Room 1',
    modalityId: 'mod-1',
    startsAt: '2026-10-10T09:00:00.000Z',
    endsAt: '2026-10-10T11:00:00.000Z',
    thumbnailId: 'thumb',
    roleCapacities: [],
  }

  function cachedActivities() {
    const client = createTestQueryClient()
    const keys = [activityKeys.list(), activityKeys.ofEvent('event-1'), activityCatalogKeys.roles()]
    for (const queryKey of keys) client.setQueryData(queryKey, [])
    const invalidated = () =>
      keys.map((queryKey) => client.getQueryState(queryKey)?.isInvalidated ?? false)
    return { client, invalidated }
  }

  it('refreshes every activity query but not the catalogs after each change', async () => {
    server.use(
      http.post('/api/activities/:eventId', () =>
        HttpResponse.json(buildActivityResponse(), { status: 201 }),
      ),
      http.put('/api/activities/:id', () => HttpResponse.json(buildActivityResponse())),
      http.delete('/api/activities/:id', () => noContent()),
      http.patch('/api/activities/:activityId/:userId/change-status', () =>
        HttpResponse.json({ id: 'status-2', name: 'Confirmed' }),
      ),
      http.patch('/api/activities/:activityId/:userId/change-role', () =>
        HttpResponse.json(buildActivityResponse()),
      ),
    )
    const runs = [
      (client: TestQueryClient) =>
        new MutationObserver(client, activityMutations.create('event-1')).mutate(input),
      (client: TestQueryClient) =>
        new MutationObserver(client, activityMutations.update()).mutate({ id: 'act-1', input }),
      (client: TestQueryClient) =>
        new MutationObserver(client, activityMutations.remove()).mutate('act-1'),
      (client: TestQueryClient) =>
        new MutationObserver(client, activityMutations.changeStatus()).mutate({
          activityId: 'act-1',
          userId: 'user-1',
          statusId: 'status-2',
        }),
      (client: TestQueryClient) =>
        new MutationObserver(client, activityMutations.changeRole()).mutate({
          activityId: 'act-1',
          userId: 'user-1',
          roleId: 'role-2',
        }),
    ]

    for (const run of runs) {
      const { client, invalidated } = cachedActivities()
      await run(client)
      expect(invalidated()).toEqual([true, true, false])
    }
  })
})
