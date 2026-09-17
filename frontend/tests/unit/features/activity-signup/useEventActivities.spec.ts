import { ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { activityQueryKeys } from '@/entities/activity'
import { useSession } from '@/entities/session'
import { useEventActivities } from '@/features/activity-signup'

import { buildActivityResponse } from '../../../support/fixtures/public-dashboard/builders'
import { mountComposable } from '../../../support/fixtures/public-dashboard/composable'
import { CHILD, ROLES, serveSignupApi } from '../../../support/fixtures/public-dashboard/signup-api'
import { t } from '../../../support/render'
import { apiError, http, HttpResponse, server, TEST_CSRF_TOKEN } from '../../../support/server'

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
    expect(result.members.value).toEqual([
      { id: '', name: t('features.activitySignup.selfMember') },
    ])
  })

  it('skips the overlap check and rejects self signups', async () => {
    const { calls } = serveSignupApi()
    const { result } = await mountSignup({ user: false })

    await expect(result.verifyOverlaps('activity-1')).resolves.toBeUndefined()
    await expect(
      result.assign.mutateAsync({
        activityId: 'activity-1',
        activityRoleTypeId: 'role-participant',
        acceptTerms: false,
      }),
    ).rejects.toThrow(t('features.activitySignup.notAuthenticated'))

    expect(calls.overlapChecks).toEqual([])
    expect(calls.assign).toEqual([])
  })
})

describe('useEventActivities for signed-in users', () => {
  it('loads assignments, household, roles and members with the user first', async () => {
    const { calls } = serveSignupApi({
      children: [CHILD],
      assigned: [
        {
          activityId: 'activity-1',
          status: { name: 'Confirmada' },
          roleType: { name: 'Participante' },
        },
      ],
      household: [
        { activityId: 'activity-1', userId: 'child-1', firstName: 'Byron', lastName: 'King' },
      ],
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

  it('fetches the terms acceptance only when the event has terms', async () => {
    const { calls, state } = serveSignupApi({ termsAccepted: false })
    const { result, hasTerms } = await mountSignup()
    await flushPromises()
    expect(calls.counts.terms).toBeUndefined()

    hasTerms.value = true
    await vi.waitFor(() => expect(result.termsAccepted.data.value).toBe(false))

    state.termsAccepted = true
    await result.termsAccepted.refetch()
    expect(result.termsAccepted.data.value).toBe(true)
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
        overlaps: [{ activityId: 'activity-9', title: 'Otra', startsAt: '2099-06-10T09:00:00Z' }],
      },
    })
    const { result } = await mountSignup()

    const check = await result.verifyOverlaps('activity-1')

    expect(calls.overlapChecks).toEqual(['activity-1/user-1'])
    expect(check).toEqual({
      hasOverlaps: true,
      overlaps: [
        { activityId: 'activity-9', title: 'Otra', startsAt: '2099-06-10T09:00:00Z', endsAt: null },
      ],
    })
  })

  it('signs the user up and refreshes activities, assignments, household and terms', async () => {
    const { calls, state } = serveSignupApi()
    const { result } = await mountSignup({ hasTerms: true })
    await vi.waitFor(() => expect(result.termsAccepted.data.value).toBe(true))
    const before = { ...calls.counts }
    state.activities = [buildActivityResponse({ title: 'Actualizada' })]

    await result.assign.mutateAsync({
      activityId: 'activity-1',
      activityRoleTypeId: 'role-participant',
      acceptTerms: true,
    })

    expect(calls.assign).toEqual([
      {
        path: 'activity-1/user-1',
        body: { activityRoleTypeId: 'role-participant', acceptTerms: true },
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
      acceptTerms: false,
    })

    expect(calls.household).toEqual([
      {
        path: 'activity-1',
        body: {
          assignments: [
            { userId: 'user-1', activityRoleTypeId: 'role-participant' },
            { userId: 'child-1', activityRoleTypeId: 'role-volunteer' },
          ],
          acceptTerms: false,
        },
      },
    ])
  })

  it('withdraws a signup and refreshes the lists but not the terms acceptance', async () => {
    const { calls } = serveSignupApi()
    const { result } = await mountSignup({ hasTerms: true })
    await vi.waitFor(() => expect(result.termsAccepted.data.value).toBe(true))
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
        activityRoleTypeId: 'role-participant',
        acceptTerms: false,
      }),
    ).rejects.toMatchObject({ status: 409 })
  })

  it('resolves no household members if the session ends before a forced refetch', async () => {
    serveSignupApi({ children: [CHILD] })
    const { result, queryClient } = await mountSignup()
    await vi.waitFor(() => expect(result.hasHousehold.value).toBe(true))

    useSession().clear()
    await queryClient.refetchQueries({
      queryKey: activityQueryKeys.householdMembers(),
      type: 'all',
    })

    expect(result.hasHousehold.value).toBe(false)
    expect(result.members.value).toEqual([
      { id: '', name: t('features.activitySignup.selfMember') },
    ])
  })
})
