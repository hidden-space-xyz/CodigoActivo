import type { EventListItemResponse, EventResponse } from '@/shared/api/generated/models'
import { formatDate, formatDateTimeRange, parseDateOnly } from '@/shared/lib'

import type { EventCategoryTag, EventDetail, PastEvent, UpcomingEvent } from '../model/types'

function toCategoryTags(event: EventListItemResponse): EventCategoryTag[] {
  return (event.categories ?? [])
    .filter((category) => category.categoryTypeId)
    .map((category) => ({
      id: category.categoryTypeId as string,
      name: category.name ?? '',
      color: category.color ?? '',
    }))
}

function isSignupOpen(event: EventListItemResponse): boolean {
  const now = Date.now()
  const start = event.signupStartsAt ? new Date(event.signupStartsAt).getTime() : null
  const end = event.signupEndsAt ? new Date(event.signupEndsAt).getTime() : null
  if (start !== null && now < start) return false
  if (end !== null && now > end) return false
  return start !== null || end !== null
}

function isSignupWindowOpen(event: EventListItemResponse): boolean {
  const now = Date.now()
  const start = event.signupStartsAt ? new Date(event.signupStartsAt).getTime() : null
  const end = event.signupEndsAt ? new Date(event.signupEndsAt).getTime() : null
  if (start === null || end === null) return false
  return now >= start && now <= end
}

export function toUpcomingEvent(event: EventListItemResponse): UpcomingEvent {
  return {
    id: event.id ?? '',
    title: event.title ?? '',
    slogan: event.subtitle ?? '',
    date: event.eventStartsAt ? formatDate(event.eventStartsAt) : 'Próximamente',
    status: isSignupOpen(event) ? 'Inscripción abierta' : 'Próximamente',
    thumbnailId: event.thumbnailId ?? '',
    categories: toCategoryTags(event),
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
  const signupLabel = formatDateTimeRange(event.signupStartsAt, event.signupEndsAt)
  return {
    id: event.id ?? '',
    title: event.title ?? '',
    subtitle: event.subtitle ?? '',
    description: event.description ?? '',
    startsAt: start ?? null,
    endsAt: end ?? null,
    dateLabel,
    signupLabel,
    status: isSignupOpen(event) ? 'Inscripción abierta' : 'Próximamente',
    thumbnailId: event.thumbnailId ?? '',
    signupOpen: isSignupWindowOpen(event),
    categories: toCategoryTags(event),
  }
}

export function toPastEvent(event: EventListItemResponse): PastEvent {
  const localStart = parseDateOnly(event.eventStartsAt)
  const year = localStart ? String(localStart.getFullYear()) : '—'
  return {
    id: event.id ?? '',
    title: event.title ?? '',
    eventName: event.subtitle ?? '',
    year,
    thumbnailId: event.thumbnailId ?? '',
    categories: toCategoryTags(event),
  }
}
