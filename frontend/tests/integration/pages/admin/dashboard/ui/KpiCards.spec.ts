import { describe, expect, it } from 'vitest'

import KpiCards from '@/pages/admin/dashboard/ui/KpiCards.vue'

import { renderWithProviders, t } from '../../../../../support/render'

async function renderTiles() {
  const { wrapper } = await renderWithProviders(KpiCards, {
    props: {
      kpis: [
        { key: 'users', total: 1200, inRange: 30, previousRange: 20 },
        { key: 'members', total: 80, inRange: 2, previousRange: 4 },
        { key: 'inscriptions', total: 500, inRange: 10, previousRange: 10 },
        { key: 'events', total: 12, inRange: 3, previousRange: 0 },
        { key: 'unknown', total: 99, inRange: 0, previousRange: 0 },
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

describe('KpiCards', () => {
  it('renders the six fixed tiles in order with their totals', async () => {
    const { tiles } = await renderTiles()

    expect(tiles.map((tile) => tile.find('.kpi-card__label').text())).toEqual([
      t('pages.admin.dashboard.kpi.users'),
      t('pages.admin.dashboard.kpi.members'),
      t('pages.admin.dashboard.kpi.inscriptions'),
      t('pages.admin.dashboard.kpi.events'),
      t('pages.admin.dashboard.kpi.resources'),
      t('pages.admin.dashboard.kpi.news'),
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
