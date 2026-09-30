import type { FormProblem, FormReading } from '@/shared/lib/form'

import type { SendEmailPayload } from './types'

/** Maximum number of files attached to one email. */
export const MAX_ATTACHMENTS = 10

/** Maximum combined attachment size in bytes (8 MiB). */
export const MAX_ATTACHMENTS_BYTES = 8 * 1024 * 1024

/** What the email dialog binds its inputs to. */
export interface EmailDraft {
  subject: string
  body: string
  attachments: File[]
}

/** Field of the email form that can be refused. */
type EmailField = 'subject' | 'body'

/** Why picked files cannot be attached. */
export type AttachmentProblem = 'tooMany' | 'tooLarge'

/** A blank email. */
export function toEmailDraft(): EmailDraft {
  return { subject: '', body: '', attachments: [] }
}

/** Reads the email dialog: subject and body are required and sent as typed. */
export function readEmailDraft(draft: EmailDraft): FormReading<EmailField, SendEmailPayload> {
  const problems: Partial<Record<EmailField, FormProblem>> = {}
  if (!draft.subject.trim()) problems.subject = 'features.sendEmail.form.problems.subjectRequired'
  if (!draft.body.trim()) problems.body = 'features.sendEmail.form.problems.bodyRequired'
  if (problems.subject || problems.body) return { problems, value: null }
  return {
    problems,
    value: { subject: draft.subject, body: draft.body, attachments: [...draft.attachments] },
  }
}

/**
 * The attachments once `picked` files are added, or why they do not fit: at most
 * `MAX_ATTACHMENTS` files of `MAX_ATTACHMENTS_BYTES` together.
 */
export function addAttachments(
  current: readonly File[],
  picked: readonly File[],
): { readonly attachments: File[] } | { readonly problem: AttachmentProblem } {
  const attachments = [...current, ...picked]
  if (attachments.length > MAX_ATTACHMENTS) return { problem: 'tooMany' }
  const size = attachments.reduce((sum, file) => sum + file.size, 0)
  if (size > MAX_ATTACHMENTS_BYTES) return { problem: 'tooLarge' }
  return { attachments }
}
