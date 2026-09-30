import { describe, expect, it } from 'vitest'

import { renderWithProviders } from '../../../../support/render'
import { SectionEyebrow } from '@/shared/ui/section-eyebrow'

describe('SectionEyebrow', () => {
  it('uses the orange ink color by default', async () => {
    const { wrapper } = await renderWithProviders(SectionEyebrow, { props: { text: 'News' } })

    expect(wrapper.text()).toBe('News')
    expect(wrapper.attributes('style')).toContain('var(--ca-orange-ink)')
  })

  it('accepts a custom color', async () => {
    const { wrapper } = await renderWithProviders(SectionEyebrow, {
      props: { text: 'News', color: 'red' },
    })

    expect(wrapper.attributes('style')).toContain('color: red')
  })
})
