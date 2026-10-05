import { computed, ref, shallowRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMutation } from '@tanstack/vue-query'

import {
  getEventAttendeesEmailAudienceRequest,
  getUsersEmailAudienceRequest,
  sendEmailToEventAttendeesRequest,
  sendEmailToUserRequest,
  sendEmailToUsersRequest,
} from '../api/requests'
import { useCrudFeedback } from '@/shared/lib/feedback'

import type { EmailAudience, RecipientFilters, SendEmailPayload, SendEmailResult } from './types'

/**
 * Mutations that email one user, all filtered users, or filtered event attendees, plus lookups of
 * who those emails would reach (`usersAudience`, `eventAttendeesAudience`). Attachments are sent
 * only when present. No toasts or cache changes; see `useSendEmailDialog` for feedback.
 */
export function useSendEmail() {
  const sendToUser = useMutation({
    mutationFn: (vars: { userId: string; payload: SendEmailPayload }) =>
      sendEmailToUserRequest(vars.userId, vars.payload),
  })

  const sendToUsers = useMutation({
    mutationFn: (vars: { params: RecipientFilters; payload: SendEmailPayload }) =>
      sendEmailToUsersRequest(vars.params, vars.payload),
  })

  const sendToEventAttendees = useMutation({
    mutationFn: (vars: { eventId: string; params: RecipientFilters; payload: SendEmailPayload }) =>
      sendEmailToEventAttendeesRequest(vars.eventId, vars.params, vars.payload),
  })

  function usersAudience(params: RecipientFilters): Promise<EmailAudience> {
    return getUsersEmailAudienceRequest(params)
  }

  function eventAttendeesAudience(
    eventId: string,
    params: RecipientFilters,
  ): Promise<EmailAudience> {
    return getEventAttendeesEmailAudienceRequest(eventId, params)
  }

  return { sendToUser, sendToUsers, sendToEventAttendees, usersAudience, eventAttendeesAudience }
}

function useSendEmailFeedback() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()

  function reportSendResult(result: SendEmailResult): void {
    const { queued, skipped } = result

    if (queued > 0)
      feedback.success(t('features.sendEmail.toast.queued', { count: queued }, queued))
    if (skipped > 0)
      feedback.warn(t('features.sendEmail.toast.skipped', { count: skipped }, skipped))
  }

  return { reportSendResult }
}

interface SendEmailHandlers {
  readonly onSuccess: (result: SendEmailResult) => void
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
  /**
   * Loads who the email would reach: one recipient, or the whole filtered audience for `null`. It
   * must select recipients exactly as the matching send does, and is skipped for a recipient
   * without id, like the send itself.
   */
  readonly fetchAudience: (recipient: T | null) => Promise<EmailAudience>
  readonly onError: (error: unknown) => void
}

/**
 * State for `SendEmailDialog` on a list page. `open(row)` targets one recipient and `open(null)`
 * the whole filtered audience. Opening also loads that audience: `recipients` counts the addresses
 * the email will reach and `withoutConsent` those without promotional consent; both stay `null`
 * while loading, after a failure or once a newer `open` superseded the request. After sending, toasts report the queued and skipped
 * counts, and the dialog closes only if at least one email was accepted for delivery.
 */
export function useSendEmailDialog<T>(options: SendEmailDialogOptions<T>) {
  const { sendToUser } = useSendEmail()
  const { reportSendResult } = useSendEmailFeedback()

  const visible = ref(false)
  const recipient = shallowRef<T | null>(null)
  const audience = shallowRef<EmailAudience | null>(null)
  const recipients = computed(() => audience.value?.recipients ?? null)
  const withoutConsent = computed(() => audience.value?.withoutConsent ?? null)
  let audienceRequest = 0

  function loadAudience(next: T | null): void {
    audienceRequest += 1
    const request = audienceRequest
    audience.value = null
    if (next !== null && !options.idOf(next)) return
    void options.fetchAudience(next).then(
      (loaded) => {
        if (request === audienceRequest) audience.value = loaded
      },
      () => undefined,
    )
  }

  const target = computed(() => {
    const current = recipient.value
    return current ? options.targetOne(current) : options.targetAll()
  })

  const sending = computed(() => sendToUser.isPending.value || options.bulkPending())

  function open(next: T | null): void {
    recipient.value = next
    visible.value = true
    loadAudience(next)
  }

  function submit(payload: SendEmailPayload): void {
    const handlers: SendEmailHandlers = {
      onSuccess: (result) => {
        if (result.queued > 0) visible.value = false
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

  return { visible, recipient, target, sending, recipients, withoutConsent, open, submit }
}
