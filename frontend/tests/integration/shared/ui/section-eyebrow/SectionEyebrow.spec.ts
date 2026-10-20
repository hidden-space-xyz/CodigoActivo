import { describe, expect, it } from 'vitest'

import { renderWithProviders } from '../../../../support/render'
import { SectionEyebrow } from '@/shared/ui/section-eyebrow'

describe('SectionEyebrow', () => {
  it('renders the kicker text', async () => {
    const { wrapper } = await renderWithProviders(SectionEyebrow, { props: { text: 'News' } })

    expect(wrapper.text()).toBe('News')
    expect(wrapper.classes()).toContain('eyebrow')
  })
})
