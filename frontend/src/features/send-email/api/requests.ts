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

export function sendEmailToUserRequest(userId: string, body: PostApiEmailsUsersUserIdBody) {
  return postApiEmailsUsersUserId(userId, body).then((r) => r.data)
}

export function sendEmailToUsersRequest(
  body: PostApiEmailsUsersBody,
  params: PostApiEmailsUsersParams,
) {
  return postApiEmailsUsers(body, params).then((r) => r.data)
}

export function sendEmailToEventAttendeesRequest(
  eventId: string,
  body: PostApiEmailsEventsEventIdAttendeesBody,
  params: PostApiEmailsEventsEventIdAttendeesParams,
) {
  return postApiEmailsEventsEventIdAttendees(eventId, body, params).then((r) => r.data)
}
