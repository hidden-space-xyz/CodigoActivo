import { describe, expect, it } from 'vitest'

import { ORGANIZATION_VALUES } from '@/pages/about/config/organization'
import ValueCard from '@/pages/about/ui/ValueCard.vue'

import { renderWithProviders, t } from '../../../../support/render'

describe('ValueCard', () => {
  it('renders every organization value with its icon, title and description', async () => {
    for (const value of ORGANIZATION_VALUES) {
      const { wrapper } = await renderWithProviders(ValueCard, { props: { value } })

      const icon = wrapper.find('.value-card__icon')
      expect(icon.text()).toBe(value.icon)
      expect(icon.attributes('style')?.replace(/\s/g, '')).toContain(`background:${value.soft}`)
      expect(wrapper.find('.value-card__title').text()).toBe(t(value.titleKey))
      expect(wrapper.find('.value-card__desc').text()).toBe(t(value.descriptionKey))
      wrapper.unmount()
    }
  })
})
