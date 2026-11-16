import { describe, expect, it } from 'vitest'

import { renderWithProviders } from '../../../../support/render'
import { AppBackground } from '@/shared/ui/app-background'

describe('AppBackground', () => {
  it('hides the decorative background from assistive technology', async () => {
    const { wrapper } = await renderWithProviders(AppBackground)

    expect(wrapper.find('.bg').attributes('aria-hidden')).toBe('true')
  })
})
