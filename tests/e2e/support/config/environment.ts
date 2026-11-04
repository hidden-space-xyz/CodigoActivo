/** Where the suite reaches the stack under test and the accounts its deployment designates. */
export const environment = {
  baseUrl: process.env.BASE_URL ?? 'http://localhost:8180',
  mailpitUrl: process.env.MAILPIT_URL ?? 'http://localhost:8125',
  initialAdminEmail: process.env.BOOTSTRAP_ADMIN_EMAIL ?? 'admin@e2e.codigoactivo.test',
  privacyOfficerEmail: process.env.PRIVACY_OFFICER_EMAIL ?? 'privacy@e2e.codigoactivo.test',
  clientAddressBase: Number(process.env.CLIENT_ADDRESS_BASE ?? '1'),
} as const

/** Password of every account the suite creates or takes over. */
export const PASSWORD = 'Playwright-e2e-2026'

/** Domain of the addresses the suite registers; `.test` never resolves to a real mailbox. */
export const EMAIL_DOMAIN = 'e2e.codigoactivo.test'

/** Time zone of the application clock (`APP_TIMEZONE`) and of the browsers. */
export const TIME_ZONE = 'Europe/Madrid'

/** Folder where the setup project leaves the shared accounts and their sessions for the workers. */
export const STATE_DIRECTORY = 'results/.state'
