import type { EventListItemResponse, EventResponse } from '@/shared/api/generated/models'
import { i18n } from '@/shared/i18n'
import { formatDateRange, formatDateTimeRange, parseDateOnly } from '@/shared/lib'

import type {
  EventCategoryTag,
  EventDetail,
  EventStatus,
  EventStatusKind,
  PastEvent,
  UpcomingEvent,
} from '../model/types'

function toCategoryTags(event: EventListItemResponse): EventCategoryTag[] {
  return (event.categories ?? [])
    .filter((category) => category.categoryTypeId)
    .map((category) => ({
      id: category.categoryTypeId as string,
      name: category.name ?? '',
      color: category.color ?? '',
    }))
}

function hasEnded(eventEndsAt?: string | null): boolean {
  const end = parseDateOnly(eventEndsAt)
  if (!end) return false
  const now = new Date()
  return end.getTime() < new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()
}

function resolveStatusKind(event: EventListItemResponse): EventStatusKind {
  if (hasEnded(event.eventEndsAt)) return 'finished'
  const now = Date.now()
  const start = event.signupStartsAt ? new Date(event.signupStartsAt).getTime() : null
  const end = event.signupEndsAt ? new Date(event.signupEndsAt).getTime() : null
  if (start === null && end === null) return 'upcoming'
  if (start !== null && now < start) return 'upcoming'
  if (end !== null && now > end) return 'signupClosed'
  return 'signupOpen'
}

const STATUS_LABEL_KEYS: Record<EventStatusKind, string> = {
  upcoming: 'entities.event.status.upcoming',
  signupOpen: 'entities.event.status.signupOpen',
  signupClosed: 'entities.event.status.signupClosed',
  finished: 'entities.event.status.finished',
}

function statusOf(kind: EventStatusKind): EventStatus {
  return { kind, label: i18n.global.t(STATUS_LABEL_KEYS[kind]) }
}

function toStatus(event: EventListItemResponse): EventStatus {
  return statusOf(resolveStatusKind(event))
}

function toEventDate(event: EventListItemResponse): string {
  return event.eventStartsAt
    ? formatDateRange(event.eventStartsAt, event.eventEndsAt)
    : i18n.global.t('entities.event.dateFallback')
}

export function toUpcomingEvent(event: EventListItemResponse): UpcomingEvent {
  return {
    id: event.id ?? '',
    title: event.title ?? '',
    slogan: event.subtitle ?? '',
    date: toEventDate(event),
    status: toStatus(event),
    thumbnailId: event.thumbnailId ?? '',
    categories: toCategoryTags(event),
  }
}

export function toEventDetail(event: EventResponse): EventDetail {
  const status = toStatus(event)
  return {
    id: event.id ?? '',
    title: event.title ?? '',
    subtitle: event.subtitle ?? '',
    description: event.description ?? '',
    startsAt: event.eventStartsAt ?? null,
    endsAt: event.eventEndsAt ?? null,
    dateLabel: toEventDate(event),
    signupLabel: formatDateTimeRange(event.signupStartsAt, event.signupEndsAt),
    status,
    thumbnailId: event.thumbnailId ?? '',
    signupOpen: status.kind === 'signupOpen',
    categories: toCategoryTags(event),
  }
}

export function toPastEvent(event: EventListItemResponse): PastEvent {
  return {
    id: event.id ?? '',
    title: event.title ?? '',
    eventName: event.subtitle ?? '',
    date: toEventDate(event),
    status: statusOf('finished'),
    thumbnailId: event.thumbnailId ?? '',
    categories: toCategoryTags(event),
  }
}
