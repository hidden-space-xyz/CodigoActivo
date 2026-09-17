import { describe, expect, it } from 'vitest'

import { logoMark } from '@/shared/branding'
import { BrandLogo } from '@/shared/ui'

import { renderWithProviders, t } from '../../../support/render'

describe('BrandLogo', () => {
  it('renders the decorative mark and the brand name', async () => {
    const { wrapper } = await renderWithProviders(BrandLogo)

    expect(wrapper.classes()).toContain('brand--md')
    const image = wrapper.find('img')
    expect(image.attributes('src')).toBe(logoMark)
    expect(image.attributes('alt')).toBe('')
    expect(wrapper.text()).toBe(`${t('layout.brandNameStart')}${t('layout.brandNameEnd')}`)
    expect(wrapper.find('.brand__accent').text()).toBe(t('layout.brandNameEnd'))
  })

  it('supports the compact size', async () => {
    const { wrapper } = await renderWithProviders(BrandLogo, { props: { size: 'sm' } })

    expect(wrapper.classes()).toContain('brand--sm')
  })
})
