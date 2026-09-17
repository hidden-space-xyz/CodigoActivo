import { describe, expect, it } from 'vitest'

import { organizationJsonLd } from '@/pages/home/model/organizationJsonLd'
import { CONTACT, FOUNDING_YEAR } from '@/shared/config'

import { t } from '../../../../support/render'

describe('organizationJsonLd', () => {
  it('describes the organization as a schema.org NGO with absolute URLs', () => {
    const origin = window.location.origin

    expect(organizationJsonLd()).toEqual({
      '@context': 'https://schema.org',
      '@type': 'NGO',
      name: t('seo.siteName'),
      url: `${origin}/`,
      logo: `${origin}/apple-touch-icon.png`,
      slogan: t('pages.home.jsonLd.slogan'),
      foundingDate: String(FOUNDING_YEAR),
      address: { '@type': 'PostalAddress', addressLocality: 'León', addressCountry: 'ES' },
      sameAs: [
        CONTACT.social.instagram,
        CONTACT.social.facebook,
        CONTACT.social.linkedin,
        CONTACT.social.youtube,
      ],
    })
  })
})
