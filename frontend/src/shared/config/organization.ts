/**
 * Public identity of the association that runs the site. Its legal identity and contact details
 * come from the API (`GET /api/legal-entity`) instead, so they stay out of the repository; the
 * name and the descriptive texts are i18n messages.
 */
export const ORGANIZATION = {
  /** Year the association was founded; shown in the home hero and the Organization JSON-LD. */
  foundingYear: 2016,
  /** City the association and its events are based in, for structured data. */
  locality: 'León',
  /** ISO 3166-1 country code, for structured data. */
  countryCode: 'ES',
  webDomain: 'codigoactivo.es',
  social: {
    instagram: 'https://www.instagram.com/codigoactivo_',
    facebook: 'https://www.facebook.com/codigoactivo',
    linkedin: 'https://www.linkedin.com/company/codigoactivo',
    youtube: 'https://www.youtube.com/@campeonatocodigoactivo9489',
  },
} as const
