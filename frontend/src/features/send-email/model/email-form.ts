import type { FormProblem, FormReading } from '@/shared/lib/form'
import { hasControlCharacter } from '@/shared/lib/navigation'

import type { EmailAudience, EmailPurpose, SendEmailPayload } from './types'

/** Maximum number of files attached to one email. */
export const MAX_ATTACHMENTS = 10

/** Maximum combined attachment size in bytes (8 MiB). */
export const MAX_ATTACHMENTS_BYTES = 8 * 1024 * 1024

/** What the email dialog binds its inputs to; `purpose` stays `null` until the admin chooses it. */
export interface EmailDraft {
  purpose: EmailPurpose | null
  subject: string
  body: string
  attachments: File[]
}

/** Field of the email form that can be refused. */
type EmailField = 'purpose' | 'subject' | 'body'

/** Why picked files cannot be attached. */
export type AttachmentProblem = 'tooMany' | 'tooLarge' | 'empty'

/** A blank email, with no purpose chosen. */
export function toEmailDraft(): EmailDraft {
  return { purpose: null, subject: '', body: '', attachments: [] }
}

/**
 * How many addresses an email reaches: a promotional one leaves out the recipients without
 * promotional consent, as the API does. Without a purpose it counts every recipient.
 */
export function reachedBy(audience: EmailAudience, purpose: EmailPurpose | null): number {
  return purpose === 'Promotional'
    ? audience.recipients - audience.withoutConsent
    : audience.recipients
}

/**
 * Reads the email dialog: the purpose must be chosen, subject and body are required and sent as
 * typed, and the subject stays on one line, without the tab a paste can bring, as the API requires.
 */
export function readEmailDraft(draft: EmailDraft): FormReading<EmailField, SendEmailPayload> {
  const problems: Partial<Record<EmailField, FormProblem>> = {}
  if (draft.purpose === null) problems.purpose = 'features.sendEmail.form.problems.purposeRequired'
  if (!draft.subject.trim()) {
    problems.subject = 'features.sendEmail.form.problems.subjectRequired'
  } else if (hasControlCharacter(draft.subject)) {
    problems.subject = 'features.sendEmail.form.problems.subjectSingleLine'
  }
  if (!draft.body.trim()) problems.body = 'features.sendEmail.form.problems.bodyRequired'
  if (draft.purpose === null || problems.subject || problems.body) return { problems, value: null }
  return {
    problems,
    value: {
      subject: draft.subject,
      body: draft.body,
      purpose: draft.purpose,
      attachments: [...draft.attachments],
    },
  }
}

/**
 * The attachments once `picked` files are added, or why they do not fit: no empty file, and at
 * most `MAX_ATTACHMENTS` files of `MAX_ATTACHMENTS_BYTES` together.
 */
export function addAttachments(
  current: readonly File[],
  picked: readonly File[],
): { readonly attachments: File[] } | { readonly problem: AttachmentProblem } {
  if (picked.some((file) => file.size === 0)) return { problem: 'empty' }
  const attachments = [...current, ...picked]
  if (attachments.length > MAX_ATTACHMENTS) return { problem: 'tooMany' }
  const size = attachments.reduce((sum, file) => sum + file.size, 0)
  if (size > MAX_ATTACHMENTS_BYTES) return { problem: 'tooLarge' }
  return { attachments }
}
