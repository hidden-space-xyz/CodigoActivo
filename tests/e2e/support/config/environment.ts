/** Where the suite reaches the stack under test and the accounts it takes over or designates. */
export const environment = {
  baseUrl: process.env.BASE_URL ?? 'http://localhost:8180',
  mailpitUrl: process.env.MAILPIT_URL ?? 'http://localhost:8125',
  initialAdminEmail: process.env.BOOTSTRAP_ADMIN_EMAIL ?? 'admin@e2e.codigoactivo.test',
  privacyOfficerEmail: 'privacy@e2e.codigoactivo.test',
  clientAddressBase: Number(process.env.CLIENT_ADDRESS_BASE ?? '1'),
} as const

/**
 * The association's identity and contact details, which `compose.yaml` gives the API in `LEGAL_*`.
 */
export const LEGAL_ENTITY = {
  name: 'Código Activo (e2e)',
  taxId: 'G00000000',
  address: 'León (e2e)',
  registry: 'Sin inscripción (e2e)',
  email: 'contacto@e2e.codigoactivo.test',
  phone: '600 00 00 00',
  phoneLink: 'tel:600000000',
} as const

/** Password of every account the suite creates or takes over. */
export const PASSWORD = 'Playwright-e2e-2026'

/** Domain of the addresses the suite registers; `.test` never resolves to a real mailbox. */
export const EMAIL_DOMAIN = 'e2e.codigoactivo.test'

/** Time zone of the application clock (`APP_TIMEZONE`) and of the browsers. */
export const TIME_ZONE = 'Europe/Madrid'

/** Folder where the setup project leaves the shared accounts and their sessions for the workers. */
export const STATE_DIRECTORY = 'results/.state'
