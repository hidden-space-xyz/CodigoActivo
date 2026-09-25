/** Year the organization was founded; shown in the home hero and the Organization JSON-LD. */
export const FOUNDING_YEAR = 2016

/** Initial and reset color of the event category forms when no color is picked. */
export const DEFAULT_CATEGORY_COLOR = '#6366F1'

/**
 * Member and Sponsor user type ids, which may sign up during the early window. Mirrors the backend
 * `DomainConstants` ids; the backend `SignupGate` enforces the rule, this only drives the UI.
 */
export const EARLY_SIGNUP_USER_TYPE_IDS: readonly string[] = [
  'b0df7ac6-1312-412f-9c2a-88e6cdfb6e1c',
  '8e0b7dc4-59d3-4c3b-9a71-4f25c6b0de88',
]

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
