import { ORGANIZATION } from '@/shared/config'
import type { Translate } from '@/shared/i18n'
import { absoluteUrl } from '@/shared/lib/seo'

/**
 * Schema.org `NGO` structured data for the home page: site name, absolute logo URL, founding year,
 * address and social profiles.
 */
export function organizationJsonLd(t: Translate): Record<string, unknown> {
  return {
    '@context': 'https://schema.org',
    '@type': 'NGO',
    name: t('seo.siteName'),
    url: absoluteUrl('/'),
    logo: absoluteUrl('/apple-touch-icon.png'),
    slogan: t('pages.home.jsonLd.slogan'),
    foundingDate: String(ORGANIZATION.foundingYear),
    address: {
      '@type': 'PostalAddress',
      addressLocality: ORGANIZATION.locality,
      addressCountry: ORGANIZATION.countryCode,
    },
    sameAs: [
      ORGANIZATION.social.instagram,
      ORGANIZATION.social.facebook,
      ORGANIZATION.social.linkedin,
      ORGANIZATION.social.youtube,
    ],
  }
}
