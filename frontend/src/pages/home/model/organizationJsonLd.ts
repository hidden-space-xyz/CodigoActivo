import { CONTACT } from '@/shared/config'
import { i18n } from '@/shared/i18n'
import { absoluteUrl } from '@/shared/lib'

export function organizationJsonLd(): Record<string, unknown> {
  return {
    '@context': 'https://schema.org',
    '@type': 'NGO',
    name: i18n.global.t('seo.siteName'),
    url: absoluteUrl('/'),
    logo: absoluteUrl('/apple-touch-icon.png'),
    slogan: i18n.global.t('pages.home.jsonLd.slogan'),
    foundingDate: '2018',
    address: {
      '@type': 'PostalAddress',
      addressLocality: 'León',
      addressCountry: 'ES',
    },
    sameAs: [CONTACT.social.instagram, CONTACT.social.facebook, CONTACT.social.linkedin],
  }
}
