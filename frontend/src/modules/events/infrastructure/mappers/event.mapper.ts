import type {
  EventDetail,
  PastEvent,
  UpcomingEvent,
} from '@/modules/events/domain/entities/event.entity'
import type { EventResponse } from '@/shared/api/generated/models'
import { formatDate, formatDateTime } from '@/shared/utils/format'

const MONTHS = ['ENE', 'FEB', 'MAR', 'ABR', 'MAY', 'JUN', 'JUL', 'AGO', 'SEP', 'OCT', 'NOV', 'DIC']

function chip(startsAt?: string | null): { day: string; month: string } {
  if (!startsAt) return { day: '∞', month: 'ONLINE' }
  const date = new Date(startsAt)
  if (Number.isNaN(date.getTime())) return { day: '∞', month: 'ONLINE' }
  return { day: String(date.getDate()).padStart(2, '0'), month: MONTHS[date.getMonth()] ?? '' }
}

function isSignupOpen(event: EventResponse): boolean {
  const now = Date.now()
  const start = event.signupStartsAt ? new Date(event.signupStartsAt).getTime() : null
  const end = event.signupEndsAt ? new Date(event.signupEndsAt).getTime() : null
  if (start !== null && now < start) return false
  if (end !== null && now > end) return false
  return start !== null || end !== null
}

/**
 * Whether sign-ups are currently allowed for the event: each configured bound is
 * enforced, and a missing bound means that side is unbounded (mirrors the
 * backend validation in ActivityService.AssignAsync).
 */
function isSignupWindowOpen(event: EventResponse): boolean {
  const now = Date.now()
  const start = event.signupStartsAt ? new Date(event.signupStartsAt).getTime() : null
  const end = event.signupEndsAt ? new Date(event.signupEndsAt).getTime() : null
  if (start !== null && now < start) return false
  if (end !== null && now > end) return false
  return true
}

export function toUpcomingEvent(event: EventResponse): UpcomingEvent {
  const { day, month } = chip(event.eventStartsAt)
  return {
    id: event.id ?? '',
    title: event.title ?? '',
    slogan: event.subtitle ?? '',
    date: event.eventStartsAt ? formatDate(event.eventStartsAt) : 'Próximamente',
    dayLabel: day,
    monthLabel: month,
    status: isSignupOpen(event) ? 'Inscripción abierta' : 'Próximamente',
    description: event.description ?? '',
    featured: event.featured ?? false,
    thumbnailId: event.thumbnailId ?? '',
  }
}

export function toEventDetail(event: EventResponse): EventDetail {
  const start = event.eventStartsAt
  const end = event.eventEndsAt
  const dateLabel = start
    ? end
      ? `${formatDateTime(start)} – ${formatDateTime(end)}`
      : formatDateTime(start)
    : 'Próximamente'
  const signupLabel =
    event.signupStartsAt || event.signupEndsAt
      ? `${formatDate(event.signupStartsAt)} – ${formatDate(event.signupEndsAt)}`
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
