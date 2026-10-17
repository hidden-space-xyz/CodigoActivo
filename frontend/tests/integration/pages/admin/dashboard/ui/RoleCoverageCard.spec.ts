import { describe, expect, it } from 'vitest'

import RoleCoverageCard from '@/pages/admin/dashboard/ui/RoleCoverageCard.vue'

import { renderWithProviders, t } from '../../../../../support/render'

describe('RoleCoverageCard', () => {
  it('shows the filled share and seats of each role', async () => {
    const { wrapper } = await renderWithProviders(RoleCoverageCard, {
      props: {
        roles: [
          { key: 'leader', confirmed: 3, desired: 4 },
          { key: 'volunteer', confirmed: 0, desired: 0 },
          { key: 'participant', confirmed: 1200, desired: 1500 },
          { key: 'other', confirmed: 1, desired: 1 },
        ],
      },
    })

    expect(wrapper.find('h2').text()).toBe(t('pages.admin.dashboard.roleCoverage.title'))
    const rows = wrapper.findAll('.coverage__role')
    expect(rows.map((row) => row.find('.coverage__name').text())).toEqual([
      t('pages.admin.dashboard.series.roles.leader'),
      t('pages.admin.dashboard.series.roles.volunteer'),
      t('pages.admin.dashboard.series.roles.participant'),
      'other',
    ])
    expect(rows[0]?.find('.coverage__pct').text()).toBe('75%')
    expect(rows[0]?.find('.coverage__figures').text()).toContain(
      t('pages.admin.dashboard.roleCoverage.seats', { confirmed: '3', desired: '4' }),
    )
    expect(rows[0]?.find('.seat-meter__fill').attributes('style')).toContain('width: 75%')
    expect(rows[1]?.find('.coverage__figures').text()).toBe(
      t('pages.admin.dashboard.roleCoverage.noCapacity'),
    )
    expect(rows[1]?.find('.seat-meter__fill').attributes('style')).toContain('width: 0%')
    expect(rows[2]?.find('.coverage__figures').text()).toContain(
      t('pages.admin.dashboard.roleCoverage.seats', { confirmed: '1200', desired: '1500' }),
    )
  })
})
