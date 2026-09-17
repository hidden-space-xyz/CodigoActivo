import { describe, expect, it } from 'vitest'

import { AppBackground } from '@/shared/ui'

import { renderWithProviders } from '../../../support/render'

describe('AppBackground', () => {
  it('renders decorative floating glyphs hidden from assistive technology', async () => {
    const { wrapper } = await renderWithProviders(AppBackground)

    expect(wrapper.find('.bg').attributes('aria-hidden')).toBe('true')
    const glyphs = wrapper.findAll('.bg__glyph')
    expect(glyphs).toHaveLength(16)
    expect(glyphs[0]?.text()).toBe('{ }')
    expect(glyphs[0]?.attributes('style')).toContain('top: 14%')
    expect(glyphs[0]?.attributes('style')).toContain('animation-duration: 22s')
  })
})
