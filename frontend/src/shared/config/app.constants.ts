/** Year the organization was founded; shown in the home hero and the Organization JSON-LD. */
export const FOUNDING_YEAR = 2016

/** Initial and reset color of the event category forms when no color is picked. */
export const DEFAULT_CATEGORY_COLOR = '#6366F1'

/**
 * Member user type id. Mirrors the backend `DomainConstants.SeedIds.UserTypes.Member` id; used to
 * decide which UI elements (e.g. the event signup statistics tab) are shown to members.
 */
export const MEMBER_USER_TYPE_ID = 'b0df7ac6-1312-412f-9c2a-88e6cdfb6e1c'

/**
 * Member and Sponsor user type ids, which may sign up during the early window. Mirrors the backend
 * `DomainConstants` ids; the backend `SignupGate` enforces the rule, this only drives the UI.
 */
export const EARLY_SIGNUP_USER_TYPE_IDS: readonly string[] = [
  MEMBER_USER_TYPE_ID,
  '8e0b7dc4-59d3-4c3b-9a71-4f25c6b0de88',
]

/**
 * Requested/Confirmed/Denied assignment status ids. Mirrors the backend
 * `DomainConstants.SeedIds.AssignmentStatusTypes` ids; used only to group and color the event
 * signup statistics by status, since the API does not send a machine-readable status key.
 */
export const ASSIGNMENT_STATUS_IDS = {
  requested: '3d717eeb-de06-44b8-b7df-2cc3e2ce5cb0',
  confirmed: '3c172c13-d238-4f0b-a61b-0a5ffc6a53ba',
  denied: '714c9041-5536-420a-8176-bf745957d80e',
} as const

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
