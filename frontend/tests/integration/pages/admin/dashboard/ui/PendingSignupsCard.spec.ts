import { describe, expect, it } from 'vitest'

import type { PendingSignups } from '@/pages/admin/dashboard/model/types'
import PendingSignupsCard from '@/pages/admin/dashboard/ui/PendingSignupsCard.vue'
import { formatDate } from '@/shared/lib/date'

import { renderWithProviders, t } from '../../../../../support/render'

const pending: PendingSignups = {
  total: 4,
  events: [
    {
      id: 'aaaaaaaa-0000-0000-0000-000000000001',
      title: 'Charla',
      pending: 1,
      oldestRequestedAt: '2099-05-01T10:00:00Z',
      nextActivityStartsAt: '2099-06-01T09:00:00Z',
    },
    {
      id: 'aaaaaaaa-0000-0000-0000-000000000002',
      title: 'Hackathon',
      pending: 3,
      oldestRequestedAt: '2099-04-15T10:00:00Z',
      nextActivityStartsAt: '2099-06-10T09:00:00Z',
    },
  ],
}

describe('PendingSignupsCard', () => {
  it('lists each event with its pending count, dates and a link to manage it', async () => {
    const { wrapper } = await renderWithProviders(PendingSignupsCard, { props: { pending } })

    expect(wrapper.find('h2').text()).toBe(t('pages.admin.dashboard.pending.title'))
    expect(wrapper.find('.pending__total').text()).toBe('4 solicitudes pendientes')
    const links = wrapper.findAll('.pending__link')
    expect(links.map((link) => link.attributes('href'))).toEqual([
      '/admin/events/aaaaaaaa-0000-0000-0000-000000000001',
      '/admin/events/aaaaaaaa-0000-0000-0000-000000000002',
    ])
    expect(links.map((link) => link.find('.pending__name').text())).toEqual(['Charla', 'Hackathon'])
    expect(links.map((link) => link.find('.pending__count').text())).toEqual([
      '1 pendiente',
      '3 pendientes',
    ])
    expect(links[0]?.find('.pending__meta').text()).toBe(
      t('pages.admin.dashboard.pending.meta', {
        next: formatDate('2099-06-01T09:00:00Z'),
        oldest: formatDate('2099-05-01T10:00:00Z'),
      }),
    )
  })

  it('uses the singular summary for one pending signup', async () => {
    const { wrapper } = await renderWithProviders(PendingSignupsCard, {
      props: { pending: { total: 1, events: pending.events.slice(0, 1) } },
    })

    expect(wrapper.find('.pending__total').text()).toBe('1 solicitud pendiente')
  })

  it('shows the empty message without pending signups', async () => {
    const { wrapper } = await renderWithProviders(PendingSignupsCard, {
      props: { pending: { total: 0, events: [] } },
    })

    expect(wrapper.find('.pending__empty').text()).toBe(t('pages.admin.dashboard.pending.empty'))
    expect(wrapper.find('.pending__total').exists()).toBe(false)
    expect(wrapper.find('.pending__list').exists()).toBe(false)
  })
})
