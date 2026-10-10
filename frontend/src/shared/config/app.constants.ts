/** Year the organization was founded; shown in the home hero and the Organization JSON-LD. */
export const FOUNDING_YEAR = 2016

/** Longest search term or text filter a list query accepts (`PageQuery.MaxTextLength`). */
export const MAX_SEARCH_LENGTH = 200

/** Largest image the API stores by default (`FileStorage:MaxSizeBytes`). */
export const MAX_IMAGE_UPLOAD_BYTES = 10 * 1024 * 1024

/** File picker filter for the only image formats the API stores, detected from their content. */
export const IMAGE_UPLOAD_ACCEPT = 'image/jpeg,image/png,image/gif,image/webp'

/** Public contact details and social profiles; `phoneHref` is the E.164 form for `tel:` links. */
export const CONTACT = {
  email: 'contacto@codigoactivo.es',
  phone: '684 39 44 90',
  phoneHref: '+34684394490',
  social: {
    instagram: 'https://www.instagram.com/codigoactivo_',
    facebook: 'https://www.facebook.com/codigoactivo',
    linkedin: 'https://www.linkedin.com/company/codigoactivo',
    youtube: 'https://www.youtube.com/@campeonatocodigoactivo9489',
  },
} as const
