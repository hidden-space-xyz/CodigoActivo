import { describe, expect, it } from 'vitest'

import { organizationJsonLd } from '@/pages/home/lib/organization-json-ld'
import { ORGANIZATION } from '@/shared/config'

import { t } from '../../../../support/render'

describe('organizationJsonLd', () => {
  it('describes the organization as a schema.org NGO with absolute URLs', () => {
    const origin = window.location.origin

    expect(organizationJsonLd(t)).toEqual({
      '@context': 'https://schema.org',
      '@type': 'NGO',
      name: t('seo.siteName'),
      url: `${origin}/`,
      logo: `${origin}/apple-touch-icon.png`,
      slogan: t('pages.home.jsonLd.slogan'),
      foundingDate: String(ORGANIZATION.foundingYear),
      address: { '@type': 'PostalAddress', addressLocality: 'León', addressCountry: 'ES' },
      sameAs: [
        ORGANIZATION.social.instagram,
        ORGANIZATION.social.facebook,
        ORGANIZATION.social.linkedin,
        ORGANIZATION.social.youtube,
      ],
    })
  })
})
