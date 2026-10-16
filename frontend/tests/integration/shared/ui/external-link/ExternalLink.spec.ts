import { describe, expect, it } from 'vitest'

import { ExternalLink } from '@/shared/ui/external-link'

import { renderWithProviders, t } from '../../../../support/render'

describe('ExternalLink', () => {
  it('opens the address in a new tab and shows it compactly', async () => {
    const href = 'https://www.example.test/docs/'
    const { wrapper } = await renderWithProviders(ExternalLink, { props: { href } })

    const link = wrapper.find('a.external-link')
    expect(link.attributes('href')).toBe(href)
    expect(link.attributes('target')).toBe('_blank')
    expect(link.attributes('rel')).toBe('noopener')
    expect(link.attributes('title')).toBe(href)
    expect(link.find('.external-link__text').text()).toBe('example.test/docs')
    expect(link.find('.external-link__icon').attributes('aria-hidden')).toBe('true')
  })

  it('tells assistive technology that the link opens a new tab', async () => {
    const { wrapper } = await renderWithProviders(ExternalLink, {
      props: { href: 'https://example.test' },
    })

    expect(wrapper.find('.external-link__hint').text()).toBe(t('common.opensInNewTab'))
    expect(wrapper.text().replace(/\s+/g, ' ')).toBe(`example.test ${t('common.opensInNewTab')}`)
  })
})
