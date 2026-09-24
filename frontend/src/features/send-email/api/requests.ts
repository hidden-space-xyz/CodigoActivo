import {
  getApiEmailsEventsEventIdAttendeesAudience,
  getApiEmailsUsersAudience,
  postApiEmailsEventsEventIdAttendees,
  postApiEmailsUsers,
  postApiEmailsUsersUserId,
} from '@/shared/api/generated/endpoints/emails/emails'
import type {
  EmailAudienceResponse,
  GetApiEmailsEventsEventIdAttendeesAudienceParams,
  GetApiEmailsUsersAudienceParams,
  PostApiEmailsEventsEventIdAttendeesBody,
  PostApiEmailsEventsEventIdAttendeesParams,
  PostApiEmailsUsersBody,
  PostApiEmailsUsersParams,
  PostApiEmailsUsersUserIdBody,
} from '@/shared/api/generated/models'

import type { EmailAudience } from '../model/types'

function toEmailAudience(audience: EmailAudienceResponse): EmailAudience {
  return {
    recipients: audience.recipients ?? 0,
    withoutConsent: audience.withoutConsent ?? 0,
  }
}

/** Queues an email for a single user (`POST /api/emails/users/{userId}`) and resolves to its counts. */
export function sendEmailToUserRequest(userId: string, body: PostApiEmailsUsersUserIdBody) {
  return postApiEmailsUsersUserId(userId, body).then((r) => r.data)
}

/**
 * Queues an email for every user matching the admin users filters (`POST /api/emails/users`) and
 * resolves to the queued and skipped counts.
 */
export function sendEmailToUsersRequest(
  body: PostApiEmailsUsersBody,
  params: PostApiEmailsUsersParams,
) {
  return postApiEmailsUsers(body, params).then((r) => r.data)
}

/**
 * Queues an email for the event attendees matching the attendee report filters
 * (`POST /api/emails/events/{eventId}/attendees`) and resolves to its counts.
 */
export function sendEmailToEventAttendeesRequest(
  eventId: string,
  body: PostApiEmailsEventsEventIdAttendeesBody,
  params: PostApiEmailsEventsEventIdAttendeesParams,
) {
  return postApiEmailsEventsEventIdAttendees(eventId, body, params).then((r) => r.data)
}

/**
 * Counts who an email to the users matching the admin users filters would reach, and how many of
 * them lack promotional consent (`GET /api/emails/users/audience`). `{ id }` targets one user.
 */
export function getUsersEmailAudienceRequest(
  params: GetApiEmailsUsersAudienceParams,
): Promise<EmailAudience> {
  return getApiEmailsUsersAudience(params).then((r) => toEmailAudience(r.data))
}

/**
 * Counts who an email to the event attendees matching the attendee report filters would reach, and
 * how many of them lack promotional consent (`GET /api/emails/events/{eventId}/attendees/audience`).
 */
export function getEventAttendeesEmailAudienceRequest(
  eventId: string,
  params: GetApiEmailsEventsEventIdAttendeesAudienceParams,
): Promise<EmailAudience> {
  return getApiEmailsEventsEventIdAttendeesAudience(eventId, params).then((r) =>
    toEmailAudience(r.data),
  )
}
