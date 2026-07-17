import { CONTACT } from '@/shared/config'
import { absoluteUrl } from '@/shared/lib'

export function organizationJsonLd(): Record<string, unknown> {
  return {
    '@context': 'https://schema.org',
    '@type': 'NGO',
    name: 'Código Activo',
    url: absoluteUrl('/'),
    logo: absoluteUrl('/apple-touch-icon.png'),
    slogan: 'Programación para tod@s',
    foundingDate: '2018',
    address: {
      '@type': 'PostalAddress',
      addressLocality: 'León',
      addressCountry: 'ES',
    },
    sameAs: [CONTACT.social.instagram, CONTACT.social.facebook, CONTACT.social.linkedin],
  }
}
