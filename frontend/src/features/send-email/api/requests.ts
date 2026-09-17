import {
  postApiEmailsEventsEventIdAttendees,
  postApiEmailsUsers,
  postApiEmailsUsersUserId,
} from '@/shared/api/generated/endpoints/emails/emails'
import type {
  PostApiEmailsEventsEventIdAttendeesBody,
  PostApiEmailsEventsEventIdAttendeesParams,
  PostApiEmailsUsersBody,
  PostApiEmailsUsersParams,
  PostApiEmailsUsersUserIdBody,
} from '@/shared/api/generated/models'

/** Emails a single user (`POST /api/emails/users/{userId}`) and resolves to the send counts. */
export function sendEmailToUserRequest(userId: string, body: PostApiEmailsUsersUserIdBody) {
  return postApiEmailsUsersUserId(userId, body).then((r) => r.data)
}

/**
 * Emails every user matching the admin users filters (`POST /api/emails/users`) and resolves to the
 * sent, failed and skipped counts.
 */
export function sendEmailToUsersRequest(
  body: PostApiEmailsUsersBody,
  params: PostApiEmailsUsersParams,
) {
  return postApiEmailsUsers(body, params).then((r) => r.data)
}

/**
 * Emails the event attendees matching the attendee report filters
 * (`POST /api/emails/events/{eventId}/attendees`) and resolves to the send counts.
 */
export function sendEmailToEventAttendeesRequest(
  eventId: string,
  body: PostApiEmailsEventsEventIdAttendeesBody,
  params: PostApiEmailsEventsEventIdAttendeesParams,
) {
  return postApiEmailsEventsEventIdAttendees(eventId, body, params).then((r) => r.data)
}
