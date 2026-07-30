import { computed, ref, shallowRef } from 'vue'
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

export interface SendEmailHandlers {
  readonly onSuccess: (result: SendEmailResultResponse) => void
  readonly onError: (error: unknown) => void
}

export interface SendEmailDialogOptions<T> {
  readonly idOf: (recipient: T) => string | undefined
  readonly targetOne: (recipient: T) => string
  readonly targetAll: () => string
  readonly bulkPending: () => boolean
  readonly sendAll: (payload: SendEmailPayload, handlers: SendEmailHandlers) => void
  readonly onError: (error: unknown) => void
}

export function useSendEmailDialog<T>(options: SendEmailDialogOptions<T>) {
  const { sendToUser } = useSendEmail()
  const { reportSendResult } = useSendEmailFeedback()

  const visible = ref(false)
  const recipient = shallowRef<T | null>(null)

  const target = computed(() => {
    const current = recipient.value
    return current ? options.targetOne(current) : options.targetAll()
  })

  const sending = computed(() => sendToUser.isPending.value || options.bulkPending())

  function open(next: T | null): void {
    recipient.value = next
    visible.value = true
  }

  function submit(payload: SendEmailPayload): void {
    const handlers: SendEmailHandlers = {
      onSuccess: (result) => {
        if ((result.sent ?? 0) > 0) visible.value = false
        reportSendResult(result)
      },
      onError: options.onError,
    }

    const current = recipient.value
    if (current) {
      const userId = options.idOf(current)
      if (!userId) return
      sendToUser.mutate({ userId, payload }, handlers)
      return
    }

    options.sendAll(payload, handlers)
  }

  return { visible, recipient, target, sending, open, submit }
}
