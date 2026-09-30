import { ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { endSession } from '@/entities/session'
import { useEventActivities } from '@/pages/event-detail/model/use-event-activities'

import { t } from '../../../../support/render'
import { apiError, http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../../../support/server'
import { buildAssignedActivity, buildHouseholdAssignment } from '../../../../support/builders'
import {
  buildSignupActivity,
  CHILD,
  ROLES,
  serveSignupApi,
  TERMS_DOCUMENT,
} from '../../../../support/api/signup'
import { mountComposable } from '../../../../support/render'

function mountSignup(options: { user?: boolean; hasTerms?: boolean; eventId?: string } = {}) {
  const eventId = ref(options.eventId ?? 'event-1')
  const hasTerms = ref(options.hasTerms ?? false)
  return mountComposable(
    () =>
      useEventActivities(
        () => eventId.value,
        () => hasTerms.value,
      ),
    options.user === false ? {} : { user: { id: 'user-1', firstName: 'Ada' } },
  ).then((mounted) => ({ ...mounted, eventId, hasTerms }))
}

describe('useEventActivities for guests', () => {
  it('loads only the public activities and exposes a guest self member', async () => {
    const { calls } = serveSignupApi()

    const { result } = await mountSignup({ user: false, hasTerms: true })
    await vi.waitFor(() => expect(result.activities.data.value).toHaveLength(1))

    expect(calls.activityQueries).toEqual(['?eventId=event-1&pageSize=100&sort=activityStartsAt'])
    expect(calls.counts).toEqual({ activities: 1 })
    expect(result.isAuthenticated.value).toBe(false)
    expect(result.userId.value).toBeNull()
    expect(result.membershipReady.value).toBe(true)
    expect(result.hasHousehold.value).toBe(false)
    expect(result.selfRoles.value).toEqual([])
    expect(result.members.value).toEqual([{ id: '', name: t('pages.eventDetail.selfMember') }])
  })

  it('skips the overlap check', async () => {
    const { calls } = serveSignupApi()
    const { result } = await mountSignup({ user: false })

    await expect(result.verifyOverlaps('activity-1')).resolves.toBeUndefined()

    expect(calls.overlapChecks).toEqual([])
  })
})

describe('useEventActivities for signed-in users', () => {
  it('loads assignments, household, roles and members with the user first', async () => {
    const { calls } = serveSignupApi({
      children: [CHILD],
      assigned: [
        buildAssignedActivity({
          activityId: 'activity-1',
          status: { id: 'status-2', name: 'Confirmada' },
        }),
      ],
      household: [buildHouseholdAssignment({ activityId: 'activity-1' })],
    })

    const { result } = await mountSignup()
    await vi.waitFor(() => expect(result.signupRoles.data.value).toHaveLength(2))
    await vi.waitFor(() => expect(result.membershipReady.value).toBe(true))

    expect(calls.childrenQueries).toEqual(['?parentId=user-1&pageSize=100&sort=firstName'])
    expect(calls.counts.terms).toBeUndefined()
    expect(result.isAuthenticated.value).toBe(true)
    expect(result.assigned.data.value).toEqual([
      { activityId: 'activity-1', status: 'Confirmada', roleName: 'Participante' },
    ])
    expect(result.household.data.value?.[0]?.name).toBe('Byron King')
    expect(result.hasHousehold.value).toBe(true)
    expect(result.members.value).toEqual([
      { id: 'user-1', name: 'Ada' },
      { id: 'child-1', name: 'Byron King' },
    ])
    expect(result.selfRoles.value).toEqual([ROLES[0]])
    expect(result.rolesFor('child-1')).toEqual(ROLES)
    expect(result.rolesFor('stranger')).toEqual([])
  })

  it('reports membership as not ready while assignments are loading', async () => {
    serveSignupApi()
    let release: (() => void) | undefined
    server.use(
      http.get('/api/me/assigned-activities', async () => {
        await new Promise<void>((resolve) => {
          release = resolve
        })
        return HttpResponse.json([])
      }),
    )

    const { result } = await mountSignup()
    await vi.waitFor(() => expect(release).toBeDefined())
    expect(result.membershipReady.value).toBe(false)

    release?.()
    await vi.waitFor(() => expect(result.membershipReady.value).toBe(true))
    expect(result.hasHousehold.value).toBe(false)
  })

  it('fetches the terms state only when the event has terms', async () => {
    const { calls, state } = serveSignupApi({ termsAccepted: false })
    const { result, hasTerms } = await mountSignup()
    await flushPromises()
    expect(calls.counts.terms).toBeUndefined()

    hasTerms.value = true
    await vi.waitFor(() => expect(result.termsState.data.value?.signupBlocked).toBe(true))

    state.termsAccepted = true
    await result.termsState.refetch()
    expect(result.termsState.data.value?.signupBlocked).toBe(false)
  })

  it('follows the event id getter', async () => {
    const { calls } = serveSignupApi()
    const { result, eventId } = await mountSignup()
    await vi.waitFor(() => expect(result.activities.data.value).toHaveLength(1))

    eventId.value = 'event-2'
    await vi.waitFor(() => expect(calls.activityQueries).toHaveLength(2))

    expect(calls.activityQueries[1]).toContain('eventId=event-2')
  })

  it('checks overlaps for the signed-in user', async () => {
    const { calls } = serveSignupApi({
      overlap: {
        hasOverlaps: true,
        overlaps: [
          {
            activityId: 'activity-9',
            title: 'Otra',
            startsAt: '2099-06-10T09:00:00Z',
            endsAt: '2099-06-10T10:00:00Z',
          },
        ],
      },
    })
    const { result } = await mountSignup()

    const check = await result.verifyOverlaps('activity-1')

    expect(calls.overlapChecks).toEqual(['activity-1/user-1'])
    expect(check).toEqual({
      hasOverlaps: true,
      overlaps: [
        {
          activityId: 'activity-9',
          title: 'Otra',
          startsAt: '2099-06-10T09:00:00Z',
          endsAt: '2099-06-10T10:00:00Z',
        },
      ],
    })
  })

  it('signs the user up and refreshes activities, assignments, household and terms', async () => {
    const { calls, state } = serveSignupApi()
    const { result } = await mountSignup({ hasTerms: true })
    await vi.waitFor(() => expect(result.termsState.data.value?.signupBlocked).toBe(false))
    const before = { ...calls.counts }
    state.activities = [buildSignupActivity({ title: 'Actualizada' })]

    await result.assign.mutateAsync({
      activityId: 'activity-1',
      roleId: 'role-participant',
      termsDecisions: [{ termsDocumentId: TERMS_DOCUMENT.termsDocumentId, accepted: true }],
    })

    expect(calls.assign).toEqual([
      {
        path: 'activity-1/user-1',
        body: {
          activityRoleTypeId: 'role-participant',
          termsDecisions: [{ termsDocumentId: TERMS_DOCUMENT.termsDocumentId, accepted: true }],
        },
        csrf: TEST_CSRF_TOKEN,
      },
    ])
    await vi.waitFor(() => expect(result.activities.data.value?.[0]?.title).toBe('Actualizada'))
    await vi.waitFor(() => expect(calls.counts.terms).toBe((before.terms ?? 0) + 1))
    expect(calls.counts.assigned).toBe((before.assigned ?? 0) + 1)
    expect(calls.counts.household).toBe((before.household ?? 0) + 1)
  })

  it('signs several household members up in one request', async () => {
    const { calls } = serveSignupApi({ children: [CHILD] })
    const { result } = await mountSignup()

    await result.assignHousehold.mutateAsync({
      activityId: 'activity-1',
      assignments: [
        { userId: 'user-1', roleId: 'role-participant' },
        { userId: 'child-1', roleId: 'role-volunteer' },
      ],
    })

    expect(calls.household).toEqual([
      {
        path: 'activity-1',
        body: {
          assignments: [
            { userId: 'user-1', activityRoleTypeId: 'role-participant' },
            { userId: 'child-1', activityRoleTypeId: 'role-volunteer' },
          ],
        },
      },
    ])
  })

  it('propagates terms decisions to the household signup request and refreshes the terms state', async () => {
    const { calls } = serveSignupApi({ children: [CHILD], termsAccepted: false })
    const { result } = await mountSignup({ hasTerms: true })
    await vi.waitFor(() => expect(result.termsState.data.value?.signupBlocked).toBe(true))
    const before = { ...calls.counts }

    await result.assignHousehold.mutateAsync({
      activityId: 'activity-1',
      assignments: [{ userId: 'child-1', roleId: 'role-volunteer' }],
      termsDecisions: [{ termsDocumentId: TERMS_DOCUMENT.termsDocumentId, accepted: true }],
    })

    expect(calls.household).toEqual([
      {
        path: 'activity-1',
        body: {
          assignments: [{ userId: 'child-1', activityRoleTypeId: 'role-volunteer' }],
          termsDecisions: [{ termsDocumentId: TERMS_DOCUMENT.termsDocumentId, accepted: true }],
        },
      },
    ])
    await vi.waitFor(() => expect(calls.counts.terms).toBe((before.terms ?? 0) + 1))
  })

  it('withdraws a signup and refreshes the lists but not the terms state', async () => {
    const { calls } = serveSignupApi()
    const { result } = await mountSignup({ hasTerms: true })
    await vi.waitFor(() => expect(result.termsState.data.value?.signupBlocked).toBe(false))
    await flushPromises()
    const before = { ...calls.counts }

    await result.unassign.mutateAsync({ activityId: 'activity-1', userId: 'child-1' })
    await vi.waitFor(() => expect(calls.counts.activities).toBe((before.activities ?? 0) + 1))
    await flushPromises()

    expect(calls.unassign).toEqual(['activity-1/child-1'])
    expect(calls.counts.terms).toBe(before.terms)
  })

  it('surfaces signup failures as API errors', async () => {
    serveSignupApi({ assignError: () => apiError(409, 'ActivityAssignmentAlreadyExists') })
    const { result } = await mountSignup()

    await expect(
      result.assign.mutateAsync({
        activityId: 'activity-1',
        roleId: 'role-participant',
      }),
    ).rejects.toMatchObject({ status: 409 })
  })

  it('forgets the household once the session ends', async () => {
    serveSignupApi({ children: [CHILD] })
    const { result, queryClient } = await mountSignup()
    await vi.waitFor(() => expect(result.hasHousehold.value).toBe(true))

    endSession(queryClient)
    await flushPromises()

    expect(result.isAuthenticated.value).toBe(false)
    expect(result.hasHousehold.value).toBe(false)
    expect(result.members.value).toEqual([{ id: '', name: t('pages.eventDetail.selfMember') }])
  })
})
