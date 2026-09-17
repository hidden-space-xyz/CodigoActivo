import { describe, expect, it } from 'vitest'

import { ActivityStep, useOrganizationContent } from '@/entities/organization'

import { renderWithProviders } from '../../../../support/render'

describe('ActivityStep', () => {
  it('renders every organization activity as a numbered, colored step', async () => {
    for (const activity of useOrganizationContent().activities) {
      const { wrapper } = await renderWithProviders(ActivityStep, { props: { activity } })

      const badge = wrapper.find('.activity__badge')
      expect(badge.text()).toBe(activity.number)
      expect(badge.attributes('style')?.replace(/\s/g, '')).toContain(`background:${activity.soft}`)
      expect(wrapper.find('.activity__title').text()).toBe(activity.title)
      expect(wrapper.find('.activity__desc').text()).toBe(activity.description)
      wrapper.unmount()
    }
  })

  it('applies the activity text color to the badge', async () => {
    const activity = {
      id: 'custom',
      title: 'Taller',
      description: 'Descripción',
      number: '09',
      color: '#123456',
      soft: 'rgba(1,2,3,0.1)',
    }
    const { wrapper } = await renderWithProviders(ActivityStep, { props: { activity } })

    expect((wrapper.find('.activity__badge').element as HTMLElement).style.color).toBe(
      'rgb(18, 52, 86)',
    )
  })
})
