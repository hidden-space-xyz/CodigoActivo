import { describe, expect, it, vi } from 'vitest'

import { SiteFooter } from '@/widgets/site-footer'
import { ORGANIZATION } from '@/shared/config'

import { renderWithProviders, t } from '../../../../support/render'
import { http, HttpResponse, server, TEST_LEGAL_ENTITY } from '../../../../support/server'

describe('SiteFooter', () => {
  it('shows the brand, tagline and contact links', async () => {
    const { wrapper } = await renderWithProviders(SiteFooter)

    expect(wrapper.find('.brand').exists()).toBe(true)
    expect(wrapper.find('.footer__tagline').text()).toBe(t('widgets.siteFooter.tagline'))
    expect(wrapper.findAll('.footer__heading').map((heading) => heading.text())).toEqual([
      t('widgets.siteFooter.contact'),
      t('widgets.siteFooter.follow'),
      t('widgets.siteFooter.legal'),
    ])
    await vi.waitFor(() =>
      expect(
        wrapper.findAll('.footer__link').map((link) => [link.text(), link.attributes('href')]),
      ).toEqual([
        [TEST_LEGAL_ENTITY.email, `mailto:${TEST_LEGAL_ENTITY.email}`],
        [TEST_LEGAL_ENTITY.phone, 'tel:987000000'],
      ]),
    )
  })

  it('leaves the phone out when the association publishes none', async () => {
    server.use(
      http.get('/api/legal-entity', () => HttpResponse.json({ ...TEST_LEGAL_ENTITY, phone: null })),
    )

    const { wrapper } = await renderWithProviders(SiteFooter)

    await vi.waitFor(() =>
      expect(wrapper.findAll('.footer__link').map((link) => link.attributes('href'))).toEqual([
        `mailto:${TEST_LEGAL_ENTITY.email}`,
      ]),
    )
  })

  it('links to the privacy and cookie policies from the legal navigation', async () => {
    const { wrapper, router } = await renderWithProviders(SiteFooter)

    const legal = wrapper.get('nav.footer__legal')
    expect(legal.attributes('aria-label')).toBe(t('widgets.siteFooter.legal'))
    const links = legal.findAll('a')
    expect(links.map((link) => [link.text(), link.attributes('href')])).toEqual([
      [t('widgets.siteFooter.privacy'), router.resolve({ name: 'privacy-policy' }).href],
      [t('widgets.siteFooter.cookies'), router.resolve({ name: 'cookie-policy' }).href],
    ])
    for (const link of links) {
      expect(link.find('.app-icon').exists()).toBe(true)
    }
  })

  it('opens social profiles in a new tab', async () => {
    const { wrapper } = await renderWithProviders(SiteFooter)

    const channels = wrapper.findAll('.footer__channel-link')
    expect(channels.map((link) => [link.text(), link.attributes('href')])).toEqual([
      [t('widgets.siteFooter.social.instagram'), ORGANIZATION.social.instagram],
      [t('widgets.siteFooter.social.facebook'), ORGANIZATION.social.facebook],
      [t('widgets.siteFooter.social.linkedIn'), ORGANIZATION.social.linkedin],
      [t('widgets.siteFooter.social.youTube'), ORGANIZATION.social.youtube],
    ])
    for (const link of channels) {
      expect(link.attributes('target')).toBe('_blank')
      expect(link.attributes('rel')).toBe('noopener')
      expect(link.find('.app-icon').exists()).toBe(true)
    }
  })
})
