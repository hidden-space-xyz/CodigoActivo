import { describe, expect, it } from 'vitest'

import { ORGANIZATION_ACTIVITIES } from '@/pages/about/config/organization'
import ActivityStep from '@/pages/about/ui/ActivityStep.vue'

import { renderWithProviders, t } from '../../../../support/render'

describe('ActivityStep', () => {
  it('renders every organization activity as a numbered, colored step', async () => {
    for (const activity of ORGANIZATION_ACTIVITIES) {
      const { wrapper } = await renderWithProviders(ActivityStep, { props: { activity } })

      const badge = wrapper.find('.activity__badge')
      expect(badge.text()).toBe(activity.number)
      expect(badge.attributes('style')?.replace(/\s/g, '')).toContain(`background:${activity.soft}`)
      expect(wrapper.find('.activity__title').text()).toBe(t(activity.titleKey))
      expect(wrapper.find('.activity__desc').text()).toBe(t(activity.descriptionKey))
      wrapper.unmount()
    }
  })

  it('applies the activity text color to the badge', async () => {
    const [first] = ORGANIZATION_ACTIVITIES
    if (!first) throw new Error('No activities')
    const activity = { ...first, number: '09', color: '#123456', soft: 'rgba(1,2,3,0.1)' }
    const { wrapper } = await renderWithProviders(ActivityStep, { props: { activity } })

    expect((wrapper.find('.activity__badge').element as HTMLElement).style.color).toBe(
      'rgb(18, 52, 86)',
    )
  })
})
