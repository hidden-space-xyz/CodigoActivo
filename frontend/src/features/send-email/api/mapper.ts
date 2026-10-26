import type {
  EmailAudienceResponse,
  PostApiEmailsUsersBody,
  SendEmailResultResponse,
} from '@/shared/api/generated/models'

import type { EmailAudience, SendEmailPayload, SendEmailResult } from '../model/types'

/** Maps how many people an email would reach and how many of them lack news consent. */
export function toEmailAudience(audience: EmailAudienceResponse): EmailAudience {
  return { recipients: audience.recipients, withoutConsent: audience.withoutConsent }
}

/** Maps how many copies of an email were queued and how many recipients were skipped. */
export function toSendEmailResult(result: SendEmailResultResponse): SendEmailResult {
  return { queued: result.queued, skipped: result.skipped }
}

/**
 * Builds the body every send endpoint takes: the trimmed subject and body, the purpose and the
 * attachments, which are left out when there are none.
 */
export function toEmailBody(payload: SendEmailPayload): PostApiEmailsUsersBody {
  const subject = payload.subject.trim()
  const body = payload.body.trim()
  const { purpose } = payload
  return payload.attachments.length > 0
    ? { subject, body, purpose, attachments: [...payload.attachments] }
    : { subject, body, purpose }
}
