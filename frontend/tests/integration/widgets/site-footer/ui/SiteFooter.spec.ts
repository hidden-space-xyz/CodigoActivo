import { describe, expect, it } from 'vitest'

import { SiteFooter } from '@/widgets/site-footer'
import { CONTACT } from '@/shared/config'

import { renderWithProviders, t } from '../../../../support/render'

describe('SiteFooter', () => {
  it('shows the brand, tagline and contact links', async () => {
    const { wrapper } = await renderWithProviders(SiteFooter)

    expect(wrapper.find('.brand').exists()).toBe(true)
    expect(wrapper.find('.footer__tagline').text()).toBe(t('widgets.siteFooter.tagline'))
    expect(wrapper.findAll('.footer__heading').map((heading) => heading.text())).toEqual([
      t('widgets.siteFooter.contact'),
      t('widgets.siteFooter.follow'),
    ])
    const contact = wrapper.findAll('.footer__link')
    expect(contact.map((link) => [link.text(), link.attributes('href')])).toEqual([
      [CONTACT.email, `mailto:${CONTACT.email}`],
      [CONTACT.phone, `tel:${CONTACT.phoneHref}`],
    ])
  })

  it('links to the cookie policy from the legal navigation', async () => {
    const { wrapper, router } = await renderWithProviders(SiteFooter)

    const legal = wrapper.get('nav.footer__legal')
    expect(legal.attributes('aria-label')).toBe(t('widgets.siteFooter.legal'))
    const link = legal.get('a')
    expect(link.text()).toBe(t('widgets.siteFooter.cookies'))
    expect(link.attributes('href')).toBe(router.resolve({ name: 'cookie-policy' }).href)
  })

  it('opens social profiles in a new tab', async () => {
    const { wrapper } = await renderWithProviders(SiteFooter)

    const channels = wrapper.findAll('.footer__channel-link')
    expect(channels.map((link) => [link.text(), link.attributes('href')])).toEqual([
      [t('widgets.siteFooter.social.instagram'), CONTACT.social.instagram],
      [t('widgets.siteFooter.social.facebook'), CONTACT.social.facebook],
      [t('widgets.siteFooter.social.linkedIn'), CONTACT.social.linkedin],
      [t('widgets.siteFooter.social.youTube'), CONTACT.social.youtube],
    ])
    for (const link of channels) {
      expect(link.attributes('target')).toBe('_blank')
      expect(link.attributes('rel')).toBe('noopener')
      expect(link.find('.app-icon').exists()).toBe(true)
    }
  })
})
