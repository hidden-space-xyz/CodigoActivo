import { useI18n } from 'vue-i18n'
import { useMutation } from '@tanstack/vue-query'

import {
  postApiEmailsEventsEventIdAttendees,
  postApiEmailsUsers,
  postApiEmailsUsersUserId,
} from '@/shared/api/generated/endpoints/emails/emails'
import type {
  PostApiEmailsEventsEventIdAttendeesParams,
  PostApiEmailsUsersBody,
  PostApiEmailsUsersParams,
  SendEmailResultResponse,
} from '@/shared/api/generated/models'
import { useCrudFeedback } from '@/shared/lib'

export const MAX_ATTACHMENTS = 10
export const MAX_ATTACHMENTS_BYTES = 8 * 1024 * 1024

export interface SendEmailPayload {
  readonly subject: string
  readonly body: string
  readonly attachments: readonly File[]
}

function toBody(payload: SendEmailPayload): PostApiEmailsUsersBody {
  const subject = payload.subject.trim()
  const body = payload.body.trim()
  return payload.attachments.length > 0
    ? { subject, body, attachments: [...payload.attachments] }
    : { subject, body }
}

export function useSendEmail() {
  const sendToUser = useMutation({
    mutationFn: (vars: { userId: string; payload: SendEmailPayload }) =>
      postApiEmailsUsersUserId(vars.userId, toBody(vars.payload)).then((r) => r.data),
  })

  const sendToUsers = useMutation({
    mutationFn: (vars: { params: PostApiEmailsUsersParams; payload: SendEmailPayload }) =>
      postApiEmailsUsers(toBody(vars.payload), vars.params).then((r) => r.data),
  })

  const sendToEventAttendees = useMutation({
    mutationFn: (vars: {
      eventId: string
      params: PostApiEmailsEventsEventIdAttendeesParams
      payload: SendEmailPayload
    }) =>
      postApiEmailsEventsEventIdAttendees(vars.eventId, toBody(vars.payload), vars.params).then(
        (r) => r.data,
      ),
  })

  return { sendToUser, sendToUsers, sendToEventAttendees }
}

export function useSendEmailFeedback() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()

  function reportSendResult(result: SendEmailResultResponse): void {
    const sent = result.sent ?? 0
    const failed = result.failed ?? 0
    const skipped = result.skipped ?? 0

    if (sent > 0) feedback.success(t('features.sendEmail.toast.sent', { count: sent }, sent))
    if (failed > 0) feedback.warn(t('features.sendEmail.toast.failed', { count: failed }, failed))
    if (skipped > 0)
      feedback.warn(t('features.sendEmail.toast.skipped', { count: skipped }, skipped))
  }

  return { reportSendResult }
}
