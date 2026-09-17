import { ElSelect } from 'element-plus'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { EventSignupStatsPanel } from '@/features/event-signup-stats'
import { ASSIGNMENT_STATUS_IDS } from '@/shared/config'
import type { EventSignupStatsResponse } from '@/shared/api/generated/models'

import { fakeCharts, resetFakeCharts } from '../../../support/fixtures/public-dashboard/chart-mock'
import { renderWithProviders, t } from '../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../support/server'

vi.mock(
  'chart.js',
  async () => (await import('../../../support/fixtures/public-dashboard/chart-mock')).chartJsModule,
)

beforeEach(() => {
  resetFakeCharts()
})

const ROLE_PARTICIPANT = { id: 'role-participant', name: 'Participante' }
const ROLE_VOLUNTEER = { id: 'role-volunteer', name: 'Voluntario' }

const STATS_BODY: EventSignupStatsResponse = {
  eventId: 'event-1',
  roles: [ROLE_PARTICIPANT, ROLE_VOLUNTEER],
  statuses: [
    { id: ASSIGNMENT_STATUS_IDS.requested, name: 'Solicitada' },
    { id: ASSIGNMENT_STATUS_IDS.confirmed, name: 'Confirmada' },
    { id: ASSIGNMENT_STATUS_IDS.denied, name: 'Rechazada' },
  ],
  activities: [
    {
      activityId: 'activity-1',
      title: 'Taller de robótica',
      startsAt: '2099-06-10T09:00:00Z',
      cells: [
        {
          activityRoleTypeId: ROLE_PARTICIPANT.id,
          assignmentStatusId: ASSIGNMENT_STATUS_IDS.requested,
          count: 2,
        },
        {
          activityRoleTypeId: ROLE_VOLUNTEER.id,
          assignmentStatusId: ASSIGNMENT_STATUS_IDS.confirmed,
          count: 5,
        },
      ],
    },
  ],
  totals: { total: 7, requested: 2, confirmed: 5, denied: 0 },
}

interface FakeBarChartData {
  datasets: { label: string; data: number[] }[]
}

function serveStats(body: EventSignupStatsResponse = STATS_BODY) {
  server.use(http.get('/api/events/:eventId/signup-stats', () => HttpResponse.json(body)))
}

function renderPanel() {
  return renderWithProviders(EventSignupStatsPanel, { props: { eventId: 'event-1' } })
}

describe('EventSignupStatsPanel rendering', () => {
  it('draws the chart and the detail table with the totals row', async () => {
    serveStats()

    const { wrapper } = await renderPanel()
    await vi.waitFor(() => expect(wrapper.find('.signup-stats__table').exists()).toBe(true))

    expect(wrapper.find('h2').text()).toBe(t('pages.eventDetail.stats.title'))
    expect(fakeCharts).toHaveLength(1)
    expect(fakeCharts[0]?.config.type).toBe('bar')

    const rows = wrapper.findAll('.signup-stats__table tbody tr')
    expect(rows).toHaveLength(2)
    expect(rows[0]?.findAll('td').map((td) => td.text())).toEqual([
      'Taller de robótica',
      'Participante',
      '2',
      '0',
      '0',
      '2',
    ])
    expect(rows[1]?.findAll('td').map((td) => td.text())).toEqual([
      'Taller de robótica',
      'Voluntario',
      '0',
      '5',
      '0',
      '5',
    ])

    const totalsRow = wrapper.find('.signup-stats__table tfoot tr')
    expect(totalsRow.findAll('td').map((td) => td.text())).toEqual([
      t('pages.eventDetail.stats.totalsRow'),
      '2',
      '5',
      '0',
      '7',
    ])
  })

  it('lists "all roles" plus every role returned by the API in the filter', async () => {
    serveStats()

    const { wrapper } = await renderPanel()
    await vi.waitFor(() => expect(wrapper.find('.signup-stats__table').exists()).toBe(true))

    const options = wrapper
      .findComponent(ElSelect)
      .findAllComponents({ name: 'ElOption' })
      .map((option) => option.props('label'))
    expect(options).toEqual([
      t('pages.eventDetail.stats.allRoles'),
      ROLE_PARTICIPANT.name,
      ROLE_VOLUNTEER.name,
    ])
  })

  it('narrows the chart to the selected role without changing the detail table', async () => {
    serveStats()

    const { wrapper } = await renderPanel()
    await vi.waitFor(() => expect(wrapper.find('.signup-stats__table').exists()).toBe(true))

    const select = wrapper.findComponent(ElSelect)
    expect(select.props('modelValue')).toBe('')

    select.vm.$emit('update:modelValue', ROLE_VOLUNTEER.id)
    await wrapper.vm.$nextTick()

    const data = fakeCharts.at(-1)?.config.data as FakeBarChartData
    expect(data.datasets.map((dataset) => dataset.data)).toEqual([[0], [5], [0]])

    // The detail table always shows every role, regardless of the chart filter.
    expect(wrapper.findAll('.signup-stats__table tbody tr')).toHaveLength(2)
  })
})

describe('EventSignupStatsPanel states', () => {
  it('shows a loading message while the statistics load', async () => {
    server.use(
      http.get('/api/events/:eventId/signup-stats', () => new Promise<never>(() => undefined)),
    )

    const { wrapper } = await renderPanel()

    expect(wrapper.find('.signup-stats__state').text()).toBe(t('pages.eventDetail.stats.loading'))
    expect(wrapper.find('.signup-stats__table').exists()).toBe(false)
  })

  it('shows an error message when the statistics request fails', async () => {
    server.use(http.get('/api/events/:eventId/signup-stats', () => apiError(500)))

    const { wrapper } = await renderPanel()

    await vi.waitFor(() =>
      expect(wrapper.find('.signup-stats__state').text()).toBe(
        t('pages.eventDetail.stats.loadError'),
      ),
    )
    expect(wrapper.find('.signup-stats__table').exists()).toBe(false)
  })

  it('tells the user the event has no activities instead of an empty chart', async () => {
    serveStats({ eventId: 'event-1', roles: [], statuses: [], activities: [], totals: {} })

    const { wrapper } = await renderPanel()

    await vi.waitFor(() =>
      expect(wrapper.find('.signup-stats__state').text()).toBe(t('pages.eventDetail.stats.empty')),
    )
    expect(wrapper.find('.signup-stats__table').exists()).toBe(false)
    expect(fakeCharts).toHaveLength(0)
  })
})

describe('EventSignupStatsPanel privacy', () => {
  it('never shows the name or email of an enrolled person, even if the API cells leaked them', async () => {
    // Simulates a backend regression: the response carries per-signup identifiers in the cells
    // alongside the legitimate aggregate fields. The client only reads the documented fields.
    const activity = STATS_BODY.activities?.[0]
    serveStats({
      ...STATS_BODY,
      activities: [
        {
          ...activity,
          cells: (activity?.cells ?? []).map((cell) => ({
            ...cell,
            userName: 'Ada Lovelace',
            email: 'ada@example.test',
          })),
        },
      ],
    })

    const { wrapper } = await renderPanel()
    await vi.waitFor(() => expect(wrapper.find('.signup-stats__table').exists()).toBe(true))

    expect(wrapper.text()).not.toContain('Ada Lovelace')
    expect(wrapper.text()).not.toContain('ada@example.test')
    expect(wrapper.text()).not.toMatch(/@/)
    // Only the activity title, role name and formatted counts are rendered.
    const row = wrapper.find('.signup-stats__table tbody tr')
    expect(row.findAll('td').map((td) => td.text())).toEqual([
      'Taller de robótica',
      'Participante',
      '2',
      '0',
      '0',
      '2',
    ])
  })
})
