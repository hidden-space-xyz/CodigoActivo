import { describe, expect, it, vi } from 'vitest'

import { useEventSignupStats } from '@/features/event-signup-stats/model/useEventSignupStats'
import { ASSIGNMENT_STATUS_IDS } from '@/shared/config'
import type { EventSignupStatsResponse } from '@/shared/api/generated/models'

import { mountComposable } from '../../../support/fixtures/public-dashboard/composable'
import { t } from '../../../support/render'
import { HttpResponse, http, server } from '../../../support/server'

const ROLE_A = { id: 'role-a', name: 'Rol A' }
const ROLE_B = { id: 'role-b', name: 'Rol B' }

/** Registers the signup-stats endpoint for `event-1` and returns the mounted composable. */
function serveStats(response: EventSignupStatsResponse) {
  server.use(http.get('/api/events/:eventId/signup-stats', () => HttpResponse.json(response)))
  return mountComposable(() => useEventSignupStats(() => 'event-1'))
}

describe('useEventSignupStats zero-filling', () => {
  it('fills cells the API omits with zero and keeps activities without any signup', async () => {
    const { result } = await serveStats({
      eventId: 'event-1',
      roles: [ROLE_A, ROLE_B],
      statuses: [
        { id: ASSIGNMENT_STATUS_IDS.requested, name: 'Solicitada' },
        { id: ASSIGNMENT_STATUS_IDS.confirmed, name: 'Confirmada' },
        { id: ASSIGNMENT_STATUS_IDS.denied, name: 'Rechazada' },
      ],
      activities: [
        {
          activityId: 'act-1',
          title: 'Actividad 1',
          startsAt: '2099-06-10T09:00:00Z',
          cells: [
            {
              activityRoleTypeId: ROLE_A.id,
              assignmentStatusId: ASSIGNMENT_STATUS_IDS.requested,
              count: 2,
            },
            {
              activityRoleTypeId: ROLE_B.id,
              assignmentStatusId: ASSIGNMENT_STATUS_IDS.confirmed,
              count: 3,
            },
          ],
        },
        {
          activityId: 'act-2',
          title: 'Actividad sin apuntados',
          startsAt: '2099-06-12T09:00:00Z',
          cells: [],
        },
      ],
      totals: { total: 5, requested: 2, confirmed: 3, denied: 0 },
    })
    await vi.waitFor(() => expect(result.isLoading.value).toBe(false))

    expect(result.isEmpty.value).toBe(false)
    expect(result.tableRows.value).toEqual([
      {
        activityId: 'act-1',
        activityTitle: 'Actividad 1',
        roleId: ROLE_A.id,
        roleName: ROLE_A.name,
        requested: 2,
        confirmed: 0,
        denied: 0,
        total: 2,
      },
      {
        activityId: 'act-1',
        activityTitle: 'Actividad 1',
        roleId: ROLE_B.id,
        roleName: ROLE_B.name,
        requested: 0,
        confirmed: 3,
        denied: 0,
        total: 3,
      },
      {
        activityId: 'act-2',
        activityTitle: 'Actividad sin apuntados',
        roleId: ROLE_A.id,
        roleName: ROLE_A.name,
        requested: 0,
        confirmed: 0,
        denied: 0,
        total: 0,
      },
      {
        activityId: 'act-2',
        activityTitle: 'Actividad sin apuntados',
        roleId: ROLE_B.id,
        roleName: ROLE_B.name,
        requested: 0,
        confirmed: 0,
        denied: 0,
        total: 0,
      },
    ])

    // Both activities are charted, including the one without any signup (all zero bars).
    expect(result.chartData.value.labels).toEqual([['Actividad 1'], ['Actividad sin apuntados']])
    const [requestedSeries, confirmedSeries, deniedSeries] = result.chartData.value.datasets
    expect(requestedSeries?.data).toEqual([2, 0])
    expect(confirmedSeries?.data).toEqual([3, 0])
    expect(deniedSeries?.data).toEqual([0, 0])
  })

  it('reports the empty state only when the event has no activities at all', async () => {
    const { result } = await serveStats({
      eventId: 'event-1',
      roles: [],
      statuses: [],
      activities: [],
      totals: { total: 0, requested: 0, confirmed: 0, denied: 0 },
    })
    await vi.waitFor(() => expect(result.isLoading.value).toBe(false))

    expect(result.isEmpty.value).toBe(true)
    expect(result.tableRows.value).toEqual([])
  })
})

describe('useEventSignupStats role filter', () => {
  async function serveTwoRoleStats() {
    return serveStats({
      eventId: 'event-1',
      roles: [ROLE_A, ROLE_B],
      statuses: [
        { id: ASSIGNMENT_STATUS_IDS.requested, name: 'Solicitada' },
        { id: ASSIGNMENT_STATUS_IDS.confirmed, name: 'Confirmada' },
        { id: ASSIGNMENT_STATUS_IDS.denied, name: 'Rechazada' },
      ],
      activities: [
        {
          activityId: 'act-1',
          title: 'Actividad 1',
          startsAt: '2099-06-10T09:00:00Z',
          cells: [
            {
              activityRoleTypeId: ROLE_A.id,
              assignmentStatusId: ASSIGNMENT_STATUS_IDS.requested,
              count: 2,
            },
            {
              activityRoleTypeId: ROLE_B.id,
              assignmentStatusId: ASSIGNMENT_STATUS_IDS.confirmed,
              count: 5,
            },
          ],
        },
      ],
      totals: { total: 7, requested: 2, confirmed: 5, denied: 0 },
    })
  }

  it('lists "all roles" first, then the API roles', async () => {
    const { result } = await serveTwoRoleStats()
    await vi.waitFor(() => expect(result.isLoading.value).toBe(false))

    expect(result.roleOptions.value).toEqual([
      { id: '', name: t('pages.eventDetail.stats.allRoles') },
      ROLE_A,
      ROLE_B,
    ])
  })

  it('aggregates every role by default and narrows the chart when a role is picked', async () => {
    const { result } = await serveTwoRoleStats()
    await vi.waitFor(() => expect(result.isLoading.value).toBe(false))

    // Default: "all roles" sums both roles for every status.
    expect(result.chartData.value.datasets[0]?.data).toEqual([2])
    expect(result.chartData.value.datasets[1]?.data).toEqual([5])

    result.roleFilter.value = ROLE_A.id
    await vi.waitFor(() => expect(result.chartData.value.datasets[0]?.data).toEqual([2]))
    expect(result.chartData.value.datasets[1]?.data).toEqual([0])

    result.roleFilter.value = ROLE_B.id
    await vi.waitFor(() => expect(result.chartData.value.datasets[1]?.data).toEqual([5]))
    expect(result.chartData.value.datasets[0]?.data).toEqual([0])
  })

  it('does not change the totals or the detail table when the role filter changes', async () => {
    const { result } = await serveTwoRoleStats()
    await vi.waitFor(() => expect(result.isLoading.value).toBe(false))
    const rowsBefore = result.tableRows.value
    const totalsBefore = result.totals.value

    result.roleFilter.value = ROLE_A.id
    await vi.waitFor(() => expect(result.chartData.value.datasets[0]?.data).toEqual([2]))

    expect(result.tableRows.value).toEqual(rowsBefore)
    expect(result.totals.value).toEqual(totalsBefore)
  })
})

describe('useEventSignupStats ordering and totals', () => {
  it('keeps the activity order sent by the API and a stable requested/confirmed/denied series order', async () => {
    const { result } = await serveStats({
      eventId: 'event-1',
      roles: [ROLE_A],
      // Statuses arrive in a different order than the chart renders them.
      statuses: [
        { id: ASSIGNMENT_STATUS_IDS.denied, name: 'Rechazada' },
        { id: ASSIGNMENT_STATUS_IDS.confirmed, name: 'Confirmada' },
        { id: ASSIGNMENT_STATUS_IDS.requested, name: 'Solicitada' },
      ],
      activities: [
        {
          activityId: 'act-z',
          title: 'Zeta',
          startsAt: '2099-06-11T09:00:00Z',
          cells: [
            {
              activityRoleTypeId: ROLE_A.id,
              assignmentStatusId: ASSIGNMENT_STATUS_IDS.denied,
              count: 1,
            },
          ],
        },
        {
          activityId: 'act-a',
          title: 'Alfa',
          startsAt: '2099-06-10T09:00:00Z',
          cells: [
            {
              activityRoleTypeId: ROLE_A.id,
              assignmentStatusId: ASSIGNMENT_STATUS_IDS.requested,
              count: 4,
            },
          ],
        },
      ],
      totals: { total: 5, requested: 4, confirmed: 0, denied: 1 },
    })
    await vi.waitFor(() => expect(result.isLoading.value).toBe(false))

    // Activities keep the API order (Zeta, then Alfa); the composable does not re-sort them.
    expect(result.chartData.value.labels).toEqual([['Zeta'], ['Alfa']])
    expect(result.tableRows.value.map((row) => row.activityId)).toEqual(['act-z', 'act-a'])

    // Series always render requested, confirmed, denied in that order regardless of API order.
    expect(result.chartData.value.datasets.map((dataset) => dataset.label)).toEqual([
      t('pages.eventDetail.stats.table.requested'),
      t('pages.eventDetail.stats.table.confirmed'),
      t('pages.eventDetail.stats.table.denied'),
    ])
  })

  it('exposes the event totals sent by the API, matching the sum of the fixture cells', async () => {
    const cells = [
      {
        activityRoleTypeId: ROLE_A.id,
        assignmentStatusId: ASSIGNMENT_STATUS_IDS.requested,
        count: 2,
      },
      {
        activityRoleTypeId: ROLE_A.id,
        assignmentStatusId: ASSIGNMENT_STATUS_IDS.confirmed,
        count: 3,
      },
      { activityRoleTypeId: ROLE_B.id, assignmentStatusId: ASSIGNMENT_STATUS_IDS.denied, count: 1 },
    ]
    const sumBy = (statusId: string) =>
      cells.filter((cell) => cell.assignmentStatusId === statusId).reduce((s, c) => s + c.count, 0)
    const totals = {
      total: cells.reduce((s, c) => s + c.count, 0),
      requested: sumBy(ASSIGNMENT_STATUS_IDS.requested),
      confirmed: sumBy(ASSIGNMENT_STATUS_IDS.confirmed),
      denied: sumBy(ASSIGNMENT_STATUS_IDS.denied),
    }

    const { result } = await serveStats({
      eventId: 'event-1',
      roles: [ROLE_A, ROLE_B],
      statuses: [
        { id: ASSIGNMENT_STATUS_IDS.requested, name: 'Solicitada' },
        { id: ASSIGNMENT_STATUS_IDS.confirmed, name: 'Confirmada' },
        { id: ASSIGNMENT_STATUS_IDS.denied, name: 'Rechazada' },
      ],
      activities: [
        { activityId: 'act-1', title: 'Actividad 1', startsAt: '2099-06-10T09:00:00Z', cells },
      ],
      totals,
    })
    await vi.waitFor(() => expect(result.isLoading.value).toBe(false))

    expect(result.totals.value).toEqual({ total: 6, requested: 2, confirmed: 3, denied: 1 })
    const tableSum = result.tableRows.value.reduce(
      (sum, row) => ({
        requested: sum.requested + row.requested,
        confirmed: sum.confirmed + row.confirmed,
        denied: sum.denied + row.denied,
      }),
      { requested: 0, confirmed: 0, denied: 0 },
    )
    expect(tableSum).toEqual({
      requested: totals.requested,
      confirmed: totals.confirmed,
      denied: totals.denied,
    })
  })

  it('defaults missing totals fields to zero while the query has no data yet', async () => {
    server.use(
      http.get('/api/events/:eventId/signup-stats', () => new Promise<never>(() => undefined)),
    )
    const { result } = await mountComposable(() => useEventSignupStats(() => 'event-1'))

    expect(result.isLoading.value).toBe(true)
    expect(result.totals.value).toEqual({ total: 0, requested: 0, confirmed: 0, denied: 0 })
    // isEmpty is only meaningful once loading finishes; the panel checks isLoading first.
    expect(result.isEmpty.value).toBe(true)
    expect(result.chartData.value).toEqual({ labels: [], datasets: [] })
    expect(result.tableRows.value).toEqual([])
    expect(result.roleOptions.value).toEqual([
      { id: '', name: t('pages.eventDetail.stats.allRoles') },
    ])
  })
})
