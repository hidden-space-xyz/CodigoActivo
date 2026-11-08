import { expect, request, type APIRequestContext } from '@playwright/test'

import { environment } from '../config/environment'
import { escapeRegExp } from '../data/formats'

/** The subjects of the automatic messages the suite reads, as `AppStrings.resx` writes them. */
export const SUBJECTS = {
  verification: 'Verifica tu cuenta de Código Activo',
  emailChange: 'Confirma tu nuevo correo de Código Activo',
  emailInUse: 'Ya tienes una cuenta en Código Activo',
  passwordReset: 'Restablece tu contraseña de Código Activo',
  loginCode: 'Tu código de acceso a Código Activo',
  accountDeletionCode: 'Confirma la eliminación de tu cuenta de Código Activo',
  emailChangeCode: 'Confirma el cambio de correo de tu cuenta de Código Activo',
  authenticatorSetupCode: 'Confirma la configuración de una app de autenticación en Código Activo',
  blockedDataCode: 'Confirma la consulta de datos bloqueados en Código Activo',
  blockedDataConsultation: 'Se ha abierto una consulta de datos bloqueados',
  securityAlert: 'Cambio de seguridad en tu cuenta de Código Activo',
  passwordLocked: 'Hemos bloqueado tu cuenta de Código Activo',
  signupConfirmed: 'Inscripción confirmada',
  signupDenied: 'Inscripción rechazada',
} as const

/** A delivered message, reduced to what the tests read. */
export interface Email {
  readonly subject: string
  readonly text: string
  readonly attachments: readonly string[]
}

/** Which message to wait for. */
export interface EmailQuery {
  readonly to: string
  readonly subject: string
  readonly sentAfter: Date
  readonly reusedAfter?: Date
}

interface MailpitSummary {
  readonly ID: string
  readonly Created: string
  readonly Subject: string
}

interface MailpitSearch {
  readonly messages: readonly MailpitSummary[] | null
}

interface MailpitMessage {
  readonly Subject: string
  readonly Text: string
  readonly Attachments: readonly { readonly FileName: string }[] | null
}

const CLOCK_SKEW_MS = 1_000
const REUSE_GRACE_MS = 5_000
const DELIVERY_TIMEOUT_MS = 30_000

/**
 * Reads the messages the API delivered to Mailpit. Codes and links are found in the plain text
 * part, which carries every code on a line of its own and every link in full.
 */
export class Mailbox {
  private constructor(private readonly http: APIRequestContext) {}

  /** Connects to the Mailpit API of the stack. */
  static async open(): Promise<Mailbox> {
    return new Mailbox(await request.newContext({ baseURL: environment.mailpitUrl }))
  }

  /**
   * Waits for the newest message to `to` whose subject starts with `subject` and that arrived after
   * `sentAfter`. When the API may reuse a code it sent shortly before instead of sending a new one,
   * `reusedAfter` accepts, once a few seconds pass without a new message, one that arrived after
   * it. A message a scheduled job sends gets a longer `timeout` than the delivery of the outbox.
   */
  async waitFor(query: EmailQuery, timeout = DELIVERY_TIMEOUT_MS): Promise<Email> {
    const started = Date.now()
    let found: Email | undefined
    await expect
      .poll(
        async () => {
          const reusable = Date.now() - started > REUSE_GRACE_MS
          found = await this.newest(query, reusable ? query.reusedAfter : undefined)
          return found
        },
        { message: `«${query.subject}» for ${query.to}`, timeout },
      )
      .toBeDefined()
    if (!found) throw new Error(`No «${query.subject}» reached ${query.to}`)
    return found
  }

  /** Whether any message to `to` with that subject arrived after `sentAfter`. */
  async received(query: EmailQuery): Promise<boolean> {
    return (await this.newest(query, undefined)) !== undefined
  }

  /** Releases the underlying request context. */
  dispose(): Promise<void> {
    return this.http.dispose()
  }

  private async newest(
    query: EmailQuery,
    reusedAfter: Date | undefined,
  ): Promise<Email | undefined> {
    const after = (reusedAfter ?? query.sentAfter).getTime() - CLOCK_SKEW_MS
    const response = await this.http.get('/api/v1/search', {
      params: { query: `to:"${query.to}"`, limit: 50 },
    })
    expect(response.ok(), 'Mailpit search').toBe(true)
    const { messages } = (await response.json()) as MailpitSearch
    const match = (messages ?? []).find(
      (message) =>
        message.Subject.startsWith(query.subject) && Date.parse(message.Created) >= after,
    )
    if (!match) return undefined
    const detail = await this.http.get(`/api/v1/message/${match.ID}`)
    expect(detail.ok(), 'Mailpit message').toBe(true)
    const message = (await detail.json()) as MailpitMessage
    return {
      subject: message.Subject,
      text: message.Text,
      attachments: (message.Attachments ?? []).map((attachment) => attachment.FileName),
    }
  }
}

/** The six-digit code a code email carries on a line of its own. */
export function codeIn(email: Email): string {
  const code = /^\s*(\d{6})\s*$/m.exec(email.text)?.[1]
  if (!code) throw new Error(`«${email.subject}» carries no code`)
  return code
}

/** The link to `path` an email carries, relative to the site so it opens on the suite's origin. */
export function linkIn(email: Email, path: string): string {
  const target = escapeRegExp(path)
  const link = new RegExp(`https?://[^/\\s]+(${target}[#?][^\\s<>"')\\]]+)`).exec(email.text)?.[1]
  if (!link) throw new Error(`«${email.subject}» carries no link to ${path}`)
  return link
}

/** The `userId` and `code` an account link (`/verify-account#userId=…&code=…`) carries. */
export function accountLinkParts(link: string): { userId: string; code: string } {
  const fragment = new URLSearchParams(link.slice(link.indexOf('#') + 1))
  const userId = fragment.get('userId')
  const code = fragment.get('code')
  if (!userId || !code) throw new Error(`${link} is not an account link`)
  return { userId, code }
}
