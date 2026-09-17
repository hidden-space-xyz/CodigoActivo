import { describe, expect, it } from 'vitest'

import { useOrganizationContent, ValueCard } from '@/entities/organization'

import { renderWithProviders } from '../../../../support/render'

describe('ValueCard', () => {
  it('renders every organization value with its icon, title and description', async () => {
    for (const value of useOrganizationContent().values) {
      const { wrapper } = await renderWithProviders(ValueCard, { props: { value } })

      const icon = wrapper.find('.value-card__icon')
      expect(icon.text()).toBe(value.icon)
      expect(icon.attributes('style')?.replace(/\s/g, '')).toContain(`background:${value.soft}`)
      expect(wrapper.find('.value-card__title').text()).toBe(value.title)
      expect(wrapper.find('.value-card__desc').text()).toBe(value.description)
      wrapper.unmount()
    }
  })
})
