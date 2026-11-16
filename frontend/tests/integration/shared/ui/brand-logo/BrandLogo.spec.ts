import { describe, expect, it } from 'vitest'

import { renderWithProviders } from '../../../../support/render'
import { BrandLogo } from '@/shared/ui/brand-logo'

describe('BrandLogo', () => {
  it('hides the mark from assistive technology so only the brand name is read', async () => {
    const { wrapper } = await renderWithProviders(BrandLogo)

    const image = wrapper.find('img')
    expect(image.attributes('alt')).toBe('')
    expect(image.attributes('aria-hidden')).toBe('true')
  })
})
