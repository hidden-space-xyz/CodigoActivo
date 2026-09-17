import type {
  EventCategoryTypeResponse,
  EventListItemResponse,
  EventResponse,
} from '@/shared/api/generated/models'
import { i18n, type TranslationKey } from '@/shared/i18n'
import { formatDateRange, formatDateTime, formatDateTimeRange, parseDateOnly } from '@/shared/lib'

import type {
  EventCategoryTag,
  EventDetail,
  EventStatus,
  EventStatusKind,
  EventTermsInfo,
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

/** Maps a category type to a tag, defaulting missing fields to empty strings. */
export function toCategoryTag(categoryType: EventCategoryTypeResponse): EventCategoryTag {
  return {
    id: categoryType.id ?? '',
    name: categoryType.name ?? '',
    color: categoryType.color ?? '',
  }
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
  const earlyStart = event.earlySignupStartsAt
    ? new Date(event.earlySignupStartsAt).getTime()
    : null
  if (start === null && end === null) return 'upcoming'
  if (start !== null && now < start) {
    return earlyStart !== null && now >= earlyStart ? 'earlySignupOpen' : 'upcoming'
  }
  if (end !== null && now > end) return 'signupClosed'
  return 'signupOpen'
}

const STATUS_LABEL_KEYS: Record<EventStatusKind, TranslationKey> = {
  upcoming: 'entities.event.status.upcoming',
  earlySignupOpen: 'entities.event.status.earlySignupOpen',
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

/**
 * Maps a list item to a card model. The status is computed from the signup window against the
 * current time, so the label reflects the moment of mapping, not of the request.
 */
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

function toTermsInfo(event: EventResponse): EventTermsInfo | null {
  const terms = event.termsDocument
  if (!terms?.id) return null
  return {
    id: terms.id,
    name: terms.name ?? '',
    description: terms.description ?? '',
  }
}

/**
 * Maps the full event to the detail page model: pre-formatted date and signup labels, derived
 * `signupOpen`/`earlySignupOpen` flags, and `terms` as `null` when no terms document is attached.
 */
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
    earlySignupLabel: event.earlySignupStartsAt ? formatDateTime(event.earlySignupStartsAt) : null,
    status,
    thumbnailId: event.thumbnailId ?? '',
    signupOpen: status.kind === 'signupOpen',
    earlySignupOpen: status.kind === 'earlySignupOpen',
    categories: toCategoryTags(event),
    terms: toTermsInfo(event),
  }
}

/** Maps a list item to a past-event card: status is always `finished`, subtitle is `eventName`. */
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
