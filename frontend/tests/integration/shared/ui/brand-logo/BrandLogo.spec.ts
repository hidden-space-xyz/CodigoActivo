import { describe, expect, it } from 'vitest'

import { logoMark } from '@/shared/branding'

import { renderWithProviders, t } from '../../../../support/render'
import { BrandLogo } from '@/shared/ui/brand-logo'

describe('BrandLogo', () => {
  it('renders the decorative mark and the brand name', async () => {
    const { wrapper } = await renderWithProviders(BrandLogo)

    expect(wrapper.classes()).toContain('brand--md')
    const image = wrapper.find('img')
    expect(image.attributes('src')).toBe(logoMark)
    expect(image.attributes('alt')).toBe('')
    expect(wrapper.text()).toBe(`${t('common.brand.nameStart')}${t('common.brand.nameEnd')}`)
    expect(wrapper.find('.brand__accent').text()).toBe(t('common.brand.nameEnd'))
  })

  it('supports the compact size', async () => {
    const { wrapper } = await renderWithProviders(BrandLogo, { props: { size: 'sm' } })

    expect(wrapper.classes()).toContain('brand--sm')
  })
})
