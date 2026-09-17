import { describe, expect, it } from 'vitest'

import AppFooter from '@/app/layouts/AppFooter.vue'
import { CONTACT } from '@/shared/config'

import { renderWithProviders, t } from '../../../support/render'

describe('AppFooter', () => {
  it('shows the brand, tagline and contact links', async () => {
    const { wrapper } = await renderWithProviders(AppFooter)

    expect(wrapper.find('.brand').exists()).toBe(true)
    expect(wrapper.find('.footer__tagline').text()).toBe(t('layout.footerTagline'))
    expect(wrapper.findAll('.footer__heading').map((heading) => heading.text())).toEqual([
      t('layout.footerContact'),
      t('layout.footerFollow'),
    ])
    const contact = wrapper.findAll('.footer__link')
    expect(contact.map((link) => [link.text(), link.attributes('href')])).toEqual([
      [CONTACT.email, `mailto:${CONTACT.email}`],
      [CONTACT.phone, `tel:${CONTACT.phoneHref}`],
    ])
  })

  it('opens social profiles in a new tab', async () => {
    const { wrapper } = await renderWithProviders(AppFooter)

    const channels = wrapper.findAll('.footer__channel-link')
    expect(channels.map((link) => [link.text(), link.attributes('href')])).toEqual([
      [t('layout.socialInstagram'), CONTACT.social.instagram],
      [t('layout.socialFacebook'), CONTACT.social.facebook],
      [t('layout.socialLinkedIn'), CONTACT.social.linkedin],
      [t('layout.socialYouTube'), CONTACT.social.youtube],
    ])
    for (const link of channels) {
      expect(link.attributes('target')).toBe('_blank')
      expect(link.attributes('rel')).toBe('noopener')
      expect(link.find('.app-icon').exists()).toBe(true)
    }
  })
})
