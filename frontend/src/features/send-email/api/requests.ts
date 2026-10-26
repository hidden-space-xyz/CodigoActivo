import {
  getApiEmailsEventsEventIdAttendeesAudience,
  getApiEmailsUsersAudience,
  postApiEmailsEventsEventIdAttendees,
  postApiEmailsUsers,
  postApiEmailsUsersUserId,
} from '@/shared/api/generated/endpoints/emails/emails'

import type {
  EmailAudience,
  RecipientFilters,
  SendEmailPayload,
  SendEmailResult,
} from '../model/types'
import { toEmailAudience, toEmailBody, toSendEmailResult } from './mapper'

/** Queues an email for a single user (`POST /api/emails/users/{userId}`). */
export async function sendEmailToUserRequest(
  userId: string,
  payload: SendEmailPayload,
): Promise<SendEmailResult> {
  const response = await postApiEmailsUsersUserId(userId, toEmailBody(payload))
  return toSendEmailResult(response.data)
}

/** Queues an email for every user matching the admin user filters (`POST /api/emails/users`). */
export async function sendEmailToUsersRequest(
  filters: RecipientFilters,
  payload: SendEmailPayload,
): Promise<SendEmailResult> {
  const response = await postApiEmailsUsers(toEmailBody(payload), filters)
  return toSendEmailResult(response.data)
}

/**
 * Queues an email for the event attendees matching the attendee report filters
 * (`POST /api/emails/events/{eventId}/attendees`).
 */
export async function sendEmailToEventAttendeesRequest(
  eventId: string,
  filters: RecipientFilters,
  payload: SendEmailPayload,
): Promise<SendEmailResult> {
  const response = await postApiEmailsEventsEventIdAttendees(eventId, toEmailBody(payload), filters)
  return toSendEmailResult(response.data)
}

/**
 * Counts who an email to the users matching the admin user filters would reach, and how many of
 * them a news email leaves out for lack of consent (`GET /api/emails/users/audience`).
 * `{ id }` targets one user.
 */
export async function getUsersEmailAudienceRequest(
  filters: RecipientFilters,
): Promise<EmailAudience> {
  const response = await getApiEmailsUsersAudience(filters)
  return toEmailAudience(response.data)
}

/**
 * Counts who an email to the event attendees matching the attendee report filters would reach, and
 * how many of them a news email leaves out for lack of consent
 * (`GET /api/emails/events/{eventId}/attendees/audience`).
 */
export async function getEventAttendeesEmailAudienceRequest(
  eventId: string,
  filters: RecipientFilters,
): Promise<EmailAudience> {
  const response = await getApiEmailsEventsEventIdAttendeesAudience(eventId, filters)
  return toEmailAudience(response.data)
}
