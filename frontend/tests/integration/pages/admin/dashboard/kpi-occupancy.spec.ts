import { describe, expect, it } from 'vitest'

import KpiCards from '@/pages/admin/dashboard/ui/KpiCards.vue'
import OccupancyCard from '@/pages/admin/dashboard/ui/OccupancyCard.vue'
import { formatDateTime } from '@/shared/lib'
import type { DashboardOccupancyResponse } from '@/shared/api/generated/models'

import { renderWithProviders, t } from '../../../../support/render'

describe('KpiCards', () => {
  async function renderTiles() {
    const { wrapper } = await renderWithProviders(KpiCards, {
      props: {
        kpis: [
          { key: 'users', total: 1200, inRange: 30, previousRange: 20 },
          { key: 'members', total: 80, inRange: 2, previousRange: 4 },
          { key: 'inscriptions', total: 500, inRange: 10, previousRange: 10 },
          { key: 'events', total: 12, inRange: 3, previousRange: 0 },
          { key: null, total: 99 },
        ],
      },
    })
    const tiles = wrapper.findAll('.kpi-card')
    const tile = (label: string) => {
      const found = tiles.find((item) => item.find('.kpi-card__label').text() === label)
      if (!found) throw new Error(`Missing tile ${label}`)
      return found
    }
    return { tiles, tile }
  }

  it('renders the six fixed tiles in order with their totals', async () => {
    const { tiles } = await renderTiles()

    expect(tiles.map((tile) => tile.find('.kpi-card__label').text())).toEqual([
      t('pages.admin.dashboard.kpi.users'),
      t('pages.admin.dashboard.kpi.members'),
      t('pages.admin.dashboard.kpi.inscriptions'),
      t('pages.admin.dashboard.kpi.events'),
      t('pages.admin.dashboard.kpi.resources'),
      t('pages.admin.dashboard.kpi.announcements'),
    ])
    expect(tiles.map((tile) => tile.find('.kpi-card__value').text())).toEqual([
      '1200',
      '80',
      '500',
      '12',
      '0',
      '0',
    ])
  })

  it('shows an upward, downward or flat trend compared with the previous range', async () => {
    const { tile } = await renderTiles()

    const users = tile(t('pages.admin.dashboard.kpi.users'))
    expect(users.find('.kpi-card__delta--up').text()).toBe('+50%')
    expect(users.attributes('style')).toContain('--accent: var(--ca-success-ink)')

    const members = tile(t('pages.admin.dashboard.kpi.members'))
    expect(members.find('.kpi-card__delta--down').text()).toBe('-50%')
    expect(members.attributes('style')).toContain('--accent: var(--ca-danger-ink)')

    const inscriptions = tile(t('pages.admin.dashboard.kpi.inscriptions'))
    expect(inscriptions.find('.kpi-card__delta--flat').text()).toBe('0%')
  })

  it('hides the trend badge without a previous value and describes new items in range', async () => {
    const { tile } = await renderTiles()

    const events = tile(t('pages.admin.dashboard.kpi.events'))
    expect(events.find('.kpi-card__delta').exists()).toBe(false)
    expect(events.find('.kpi-card__foot').text()).toBe(
      t('pages.admin.dashboard.kpi.inRange', { n: '3' }),
    )

    const resources = tile(t('pages.admin.dashboard.kpi.resources'))
    expect(resources.find('.kpi-card__delta').exists()).toBe(false)
    expect(resources.find('.kpi-card__foot').text()).toBe(t('pages.admin.dashboard.kpi.noNew'))
  })
})

describe('OccupancyCard', () => {
  const occupancy: DashboardOccupancyResponse = {
    confirmed: 45,
    desired: 60,
    events: [
      {
        eventId: 'event-1',
        title: 'Hackathon',
        confirmed: 30,
        desired: 40,
        activities: [
          {
            activityId: 'activity-1',
            title: 'Robótica',
            startsAt: '2099-06-10T09:00:00Z',
            confirmed: 12,
            desired: 10,
          },
          { activityId: 'activity-2', title: 'Sin aforo', confirmed: 3, desired: 0 },
        ],
      },
      { eventId: 'event-2', title: 'Campamento', confirmed: 50, desired: 20 },
      { title: 'Sin id', confirmed: 1 },
    ],
  }

  it('summarizes the overall occupancy and lists events with their percentage', async () => {
    const { wrapper } = await renderWithProviders(OccupancyCard, { props: { occupancy } })

    const summary = wrapper.find('.occupancy__overall')
    expect(summary.find('strong').text()).toBe('75%')
    expect(summary.text()).toContain('45')
    expect(summary.text()).toContain('60')

    const rows = wrapper.findAll('.occupancy__row')
    expect(rows.map((row) => row.find('.occupancy__name').text())).toEqual([
      'Hackathon',
      'Campamento',
      'Sin id',
    ])
    expect(rows.map((row) => row.find('.occupancy__pct').text())).toEqual(['75%', '250%', '—%'])
    expect(rows[0]?.find('.occupancy__fill').attributes('style')).toContain('width: 75%')
    expect(rows[1]?.find('.occupancy__fill').classes()).toContain('occupancy__fill--over')
    expect(rows[1]?.find('.occupancy__fill').attributes('style')).toContain('width: 100%')
    expect(rows[2]?.find('.occupancy__fill').attributes('style')).toContain('width: 0%')
  })

  it('expands and collapses the activities of an event', async () => {
    const { wrapper } = await renderWithProviders(OccupancyCard, { props: { occupancy } })
    const firstRow = () => wrapper.findAll('.occupancy__row')[0]

    expect(wrapper.find('.occupancy__activities').exists()).toBe(false)
    await firstRow()?.trigger('click')

    const activities = wrapper.findAll('.occupancy__activity')
    expect(activities).toHaveLength(2)
    expect(activities[0]?.find('.occupancy__activity-name').text()).toBe('Robótica')
    expect(activities[0]?.find('.occupancy__activity-date').text()).toBe(
      formatDateTime('2099-06-10T09:00:00Z'),
    )
    expect(activities[0]?.find('.occupancy__activity-pct').text()).toContain('120%')
    expect(activities[0]?.find('.occupancy__activity-plazas').text()).toBe('(12/10)')
    expect(activities[0]?.find('.occupancy__fill').classes()).toContain('occupancy__fill--over')
    expect(activities[1]?.find('.occupancy__activity-pct').text()).toContain('—%')
    expect(activities[1]?.find('.occupancy__activity-date').text()).toBe('—')

    await firstRow()?.trigger('click')
    expect(wrapper.find('.occupancy__activities').exists()).toBe(false)
  })

  it('does not expand events without id or without activities', async () => {
    const { wrapper } = await renderWithProviders(OccupancyCard, { props: { occupancy } })
    const rows = wrapper.findAll('.occupancy__row')

    await rows[2]?.trigger('click')
    expect(wrapper.find('.occupancy__activities').exists()).toBe(false)

    await rows[1]?.trigger('click')
    expect(wrapper.find('.occupancy__activities').exists()).toBe(true)
    expect(wrapper.findAll('.occupancy__activity')).toHaveLength(0)
  })

  it('shows the empty message and no summary without desired places or events', async () => {
    const { wrapper } = await renderWithProviders(OccupancyCard, {
      props: { occupancy: { confirmed: 0, desired: 0, events: null } },
    })

    expect(wrapper.find('.occupancy__overall').exists()).toBe(false)
    expect(wrapper.find('.occupancy__empty').text()).toBe(
      t('pages.admin.dashboard.occupancy.empty'),
    )
  })

  it('treats missing confirmed counts and titles as zero and empty', async () => {
    const { wrapper } = await renderWithProviders(OccupancyCard, {
      props: {
        occupancy: {
          desired: 10,
          events: [{ eventId: 'event-9', desired: 5, activities: [{ desired: 4 }] }],
        },
      },
    })

    expect(wrapper.find('.occupancy__overall strong').text()).toBe('0%')
    const row = wrapper.find('.occupancy__row')
    expect(row.find('.occupancy__name').attributes('title')).toBe('')
    expect(row.find('.occupancy__pct').text()).toBe('0%')

    await row.trigger('click')
    const activity = wrapper.find('.occupancy__activity')
    expect(activity.find('.occupancy__activity-name').attributes('title')).toBe('')
    expect(activity.find('.occupancy__activity-plazas').text()).toBe('(—/4)')
  })

  it('treats a missing occupancy as empty', async () => {
    const { wrapper } = await renderWithProviders(OccupancyCard, { props: { occupancy: {} } })

    expect(wrapper.find('.occupancy__overall').exists()).toBe(false)
    expect(wrapper.find('.occupancy__empty').exists()).toBe(true)
  })
})
