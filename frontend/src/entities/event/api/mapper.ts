import type { EventResponse } from '@/shared/api/generated/models'
import { formatDate, formatDateTime } from '@/shared/lib'

import type { EventDetail, PastEvent, UpcomingEvent } from '../model/types'

function isSignupOpen(event: EventResponse): boolean {
  const now = Date.now()
  const start = event.signupStartsAt ? new Date(event.signupStartsAt).getTime() : null
  const end = event.signupEndsAt ? new Date(event.signupEndsAt).getTime() : null
  if (start !== null && now < start) return false
  if (end !== null && now > end) return false
  return start !== null || end !== null
}

function isSignupWindowOpen(event: EventResponse): boolean {
  const now = Date.now()
  const start = event.signupStartsAt ? new Date(event.signupStartsAt).getTime() : null
  const end = event.signupEndsAt ? new Date(event.signupEndsAt).getTime() : null
  if (start === null || end === null) return false
  return now >= start && now <= end
}

export function toUpcomingEvent(event: EventResponse): UpcomingEvent {
  return {
    id: event.id ?? '',
    title: event.title ?? '',
    slogan: event.subtitle ?? '',
    date: event.eventStartsAt ? formatDate(event.eventStartsAt) : 'Próximamente',
    status: isSignupOpen(event) ? 'Inscripción abierta' : 'Próximamente',
    thumbnailId: event.thumbnailId ?? '',
  }
}

export function toEventDetail(event: EventResponse): EventDetail {
  const start = event.eventStartsAt
  const end = event.eventEndsAt
  const dateLabel = start
    ? end
      ? `${formatDate(start)} – ${formatDate(end)}`
      : formatDate(start)
    : 'Próximamente'
  const signupLabel =
    event.signupStartsAt || event.signupEndsAt
      ? `${formatDateTime(event.signupStartsAt)} – ${formatDateTime(event.signupEndsAt)}`
      : '—'
  return {
    id: event.id ?? '',
    title: event.title ?? '',
    subtitle: event.subtitle ?? '',
    description: event.description ?? '',
    dateLabel,
    signupLabel,
    status: isSignupOpen(event) ? 'Inscripción abierta' : 'Próximamente',
    thumbnailId: event.thumbnailId ?? '',
    signupOpen: isSignupWindowOpen(event),
  }
}

export function toPastEvent(event: EventResponse): PastEvent {
  const year = event.eventStartsAt ? String(new Date(event.eventStartsAt).getFullYear()) : '—'
  return {
    id: event.id ?? '',
    title: event.title ?? '',
    eventName: event.subtitle ?? '',
    year,
    thumbnailId: event.thumbnailId ?? '',
  }
}
