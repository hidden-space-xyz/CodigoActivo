import { describe, expect, it } from 'vitest'

import type { Occupancy } from '@/pages/admin/dashboard/model/types'
import OccupancyCard from '@/pages/admin/dashboard/ui/OccupancyCard.vue'
import { formatDateTime } from '@/shared/lib/date'

import { renderWithProviders, t } from '../../../../../support/render'

const occupancy: Occupancy = {
  confirmed: 50,
  desired: 60,
  events: [
    {
      id: 'event-1',
      title: 'Hackathon',
      confirmed: 30,
      desired: 40,
      activities: [
        {
          id: 'activity-1',
          title: 'Robótica',
          startsAt: '2099-06-10T09:00:00Z',
          confirmed: 10,
          desired: 10,
        },
        {
          id: 'activity-2',
          title: 'Sin aforo',
          startsAt: '2099-06-10T12:00:00Z',
          confirmed: 0,
          desired: 0,
        },
      ],
    },
    { id: 'event-2', title: 'Campamento', confirmed: 20, desired: 20, activities: [] },
    { id: 'event-3', title: 'Sin aforo', confirmed: 0, desired: 0, activities: [] },
  ],
}

describe('OccupancyCard', () => {
  it('summarizes the overall occupancy and lists events with their percentage', async () => {
    const { wrapper } = await renderWithProviders(OccupancyCard, { props: { occupancy } })

    const summary = wrapper.find('.occupancy__overall')
    expect(summary.find('strong').text()).toBe('83%')
    expect(summary.text()).toContain('50')
    expect(summary.text()).toContain('60')

    const rows = wrapper.findAll('.occupancy__row')
    expect(rows.map((row) => row.find('.occupancy__name').text())).toEqual([
      'Hackathon',
      'Campamento',
      'Sin aforo',
    ])
    expect(rows.map((row) => row.find('.occupancy__pct').text())).toEqual(['75%', '100%', '—%'])
    expect(rows[0]?.find('.occupancy__fill').attributes('style')).toContain('width: 75%')
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
    expect(activities[0]?.find('.occupancy__activity-pct').text()).toContain('100%')
    expect(activities[0]?.find('.occupancy__activity-plazas').text()).toBe('(10/10)')
    expect(activities[0]?.find('.occupancy__fill').attributes('style')).toContain('width: 100%')
    expect(activities[1]?.find('.occupancy__activity-pct').text()).toContain('—%')

    await firstRow()?.trigger('click')
    expect(wrapper.find('.occupancy__activities').exists()).toBe(false)
  })

  it('expands an event without activities into an empty list', async () => {
    const { wrapper } = await renderWithProviders(OccupancyCard, { props: { occupancy } })

    await wrapper.findAll('.occupancy__row')[1]?.trigger('click')

    expect(wrapper.find('.occupancy__activities').exists()).toBe(true)
    expect(wrapper.findAll('.occupancy__activity')).toHaveLength(0)
  })

  it('shows the empty message and no summary without desired places or events', async () => {
    const { wrapper } = await renderWithProviders(OccupancyCard, {
      props: { occupancy: { confirmed: 0, desired: 0, events: [] } },
    })

    expect(wrapper.find('.occupancy__overall').exists()).toBe(false)
    expect(wrapper.find('.occupancy__empty').text()).toBe(
      t('pages.admin.dashboard.occupancy.empty'),
    )
  })
})
