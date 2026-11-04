import { expect, type Locator, type Page } from '@playwright/test'

import type { UploadFile } from '../../api/api-client'
import { confirm, setChecked } from '../../ui/element-plus'

/** The kind of email the dialog sends. */
export type EmailPurpose = 'Aviso' | 'Novedades'

/** What the email dialog is filled in with. */
export interface EmailDraft {
  readonly purpose: EmailPurpose
  readonly subject: string
  readonly body: string
  readonly attachments?: readonly UploadFile[]
}

/**
 * The dialog «Enviar correo» (`features/send-email`): the recipients and how many addresses the
 * chosen kind of email reaches, the kind, subject, message and attachments, and the confirmation
 * «¿Enviar el correo?» that sends it.
 */
export class SendEmailDialog {
  /** The dialog box titled «Enviar correo». */
  readonly root: Locator

  /** The subject field. */
  readonly subject: Locator

  /** The message field. */
  readonly body: Locator

  /** The button that asks to confirm sending. */
  readonly send: Locator

  /** The list of the attached files. */
  readonly attachments: Locator

  /** The confirmation box «¿Enviar el correo?». */
  readonly confirmation: Locator

  constructor(private readonly page: Page) {
    this.root = page.getByRole('dialog', { name: 'Enviar correo' })
    this.confirmation = page.getByRole('dialog', { name: '¿Enviar el correo?' })
    this.subject = this.root.getByLabel('Asunto')
    this.body = this.root.getByLabel('Mensaje')
    this.send = this.root.getByRole('button', { name: 'Enviar', exact: true })
    this.attachments = this.root.locator('.attachments__list')
  }

  /** The radio of the kind of email `purpose`. */
  purpose(purpose: EmailPurpose): Locator {
    return this.root.getByRole('radio', { name: purpose })
  }

  /** Chooses the kind of email. */
  async choose(purpose: EmailPurpose): Promise<void> {
    await setChecked(this.purpose(purpose), true)
  }

  /** Attaches `file` through the hidden input behind «Adjuntar archivos». */
  async attach(file: UploadFile): Promise<void> {
    await this.root.locator('input.attachments__input').setInputFiles(file)
    await expect(this.attachments.getByText(file.name, { exact: true })).toBeVisible()
  }

  /** Fills in every field of the dialog. */
  async fill(draft: EmailDraft): Promise<void> {
    await this.choose(draft.purpose)
    await this.subject.fill(draft.subject)
    await this.body.fill(draft.body)
    for (const file of draft.attachments ?? []) await this.attach(file)
  }

  /** Sends the email and confirms it in «¿Enviar el correo?», checking what it says first. */
  async sendConfirmed(message: string | RegExp): Promise<void> {
    await this.send.click()
    await expect(this.confirmation).toContainText(message)
    await confirm(this.page, '¿Enviar el correo?', 'Enviar')
  }
}
