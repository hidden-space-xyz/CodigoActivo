import { computed, ref, shallowRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMutation } from '@tanstack/vue-query'

import type {
  PostApiEmailsEventsEventIdAttendeesParams,
  PostApiEmailsUsersBody,
  PostApiEmailsUsersParams,
  SendEmailResultResponse,
} from '@/shared/api/generated/models'
import { useCrudFeedback } from '@/shared/lib'

import {
  sendEmailToEventAttendeesRequest,
  sendEmailToUserRequest,
  sendEmailToUsersRequest,
} from '../api/requests'

/** Maximum number of files attached to one email. */
export const MAX_ATTACHMENTS = 10
/** Maximum combined attachment size in bytes (8 MiB). */
export const MAX_ATTACHMENTS_BYTES = 8 * 1024 * 1024

/** Email composed in the dialog; subject and body are trimmed before sending. */
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

/**
 * Mutations that email one user, all filtered users, or filtered event attendees. Attachments are
 * sent only when present. No toasts or cache changes; see `useSendEmailDialog` for feedback.
 */
export function useSendEmail() {
  const sendToUser = useMutation({
    mutationFn: (vars: { userId: string; payload: SendEmailPayload }) =>
      sendEmailToUserRequest(vars.userId, toBody(vars.payload)),
  })

  const sendToUsers = useMutation({
    mutationFn: (vars: { params: PostApiEmailsUsersParams; payload: SendEmailPayload }) =>
      sendEmailToUsersRequest(toBody(vars.payload), vars.params),
  })

  const sendToEventAttendees = useMutation({
    mutationFn: (vars: {
      eventId: string
      params: PostApiEmailsEventsEventIdAttendeesParams
      payload: SendEmailPayload
    }) => sendEmailToEventAttendeesRequest(vars.eventId, toBody(vars.payload), vars.params),
  })

  return { sendToUser, sendToUsers, sendToEventAttendees }
}

function useSendEmailFeedback() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()

  function reportSendResult(result: SendEmailResultResponse): void {
    const queued = result.queued ?? 0
    const skipped = result.skipped ?? 0

    if (queued > 0)
      feedback.success(t('features.sendEmail.toast.queued', { count: queued }, queued))
    if (skipped > 0)
      feedback.warn(t('features.sendEmail.toast.skipped', { count: skipped }, skipped))
  }

  return { reportSendResult }
}

interface SendEmailHandlers {
  readonly onSuccess: (result: SendEmailResultResponse) => void
  readonly onError: (error: unknown) => void
}

/** How a list page plugs its rows and bulk send into `useSendEmailDialog`. */
export interface SendEmailDialogOptions<T> {
  /** User id to email; when `undefined` the single-recipient send is skipped. */
  readonly idOf: (recipient: T) => string | undefined
  /** Localized recipient description shown and confirmed in the dialog for one row. */
  readonly targetOne: (recipient: T) => string
  /** Localized description of the bulk audience, e.g. the filtered row count. */
  readonly targetAll: () => string
  /** Whether the page's bulk mutation is in flight. */
  readonly bulkPending: () => boolean
  /** Sends to the page's current filtered audience, calling `handlers` when it settles. */
  readonly sendAll: (payload: SendEmailPayload, handlers: SendEmailHandlers) => void
  readonly onError: (error: unknown) => void
}

/**
 * State for `SendEmailDialog` on a list page. `open(row)` targets one recipient and `open(null)`
 * the whole filtered audience. After sending, toasts report the queued and skipped counts, and the
 * dialog closes only if at least one email was accepted for delivery.
 */
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
        if ((result.queued ?? 0) > 0) visible.value = false
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
