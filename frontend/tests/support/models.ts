import type { AccountCertificate } from '@/entities/account'
import type { ActivityDetail, ActivityListing } from '@/entities/activity'
import type { NewsItem, NewsSummary } from '@/entities/news-item'
import type { EventDetail, EventListing, EventSummary } from '@/entities/event'
import type { Partner } from '@/entities/partner'
import type { LearningResource, LearningResourceSummary } from '@/entities/resource'
import type { AuthUser } from '@/entities/session'

import { richText } from './builders/common'

/** Signed-in session user with sensible defaults. */
export function buildAuthUser(overrides: Partial<AuthUser> = {}): AuthUser {
  return {
    id: 'user-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    email: 'ada@example.test',
    phone: '600000000',
    isAdmin: false,
    earlySignupEligible: false,
    twoFactorMethod: 'Email',
    ...overrides,
  }
}

/** Mapped certificate as the account page consumes it. */
export function buildCertificate(overrides: Partial<AccountCertificate> = {}): AccountCertificate {
  return {
    code: 'CA-2025-0001',
    eventId: 'event-1',
    participantId: 'user-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    isSelf: true,
    eventTitle: 'Hackathon de Primavera',
    eventSubtitle: 'Edición 2025',
    startsAt: '2025-05-10',
    endsAt: '2025-05-12',
    ...overrides,
  }
}

/** Card model of an upcoming event with open signup, two categories and a thumbnail. */
export function buildEventSummary(overrides: Partial<EventSummary> = {}): EventSummary {
  return {
    id: 'event-1',
    title: 'Día Código Activo',
    subtitle: 'Programa tu futuro',
    startsAt: '2026-10-03',
    endsAt: '2026-10-03',
    status: 'signupOpen',
    thumbnailId: 'thumb-1',
    categories: [
      { id: 'cat-1', name: 'Robótica', color: '#ff6b5e' },
      { id: 'cat-2', name: 'IA', color: '' },
    ],
    ...overrides,
  }
}

/** Card model of a finished event. */
export function buildPastEventSummary(overrides: Partial<EventSummary> = {}): EventSummary {
  return buildEventSummary({
    id: 'event-0',
    title: 'Edición 2025',
    subtitle: 'Crea y comparte',
    startsAt: '2025-05-10',
    endsAt: '2025-05-10',
    status: 'finished',
    thumbnailId: 'thumb-0',
    categories: [{ id: 'cat-1', name: 'Robótica', color: '#ff6b5e' }],
    ...overrides,
  })
}

/** News card model. */
export function buildNewsSummary(overrides: Partial<NewsSummary> = {}): NewsSummary {
  return {
    id: 'news-item-1',
    title: 'Abrimos inscripciones',
    subtitle: 'Plazas limitadas',
    createdAt: '2026-03-15T10:00:00Z',
    thumbnailId: 'thumb-a',
    featured: false,
    ...overrides,
  }
}

/** Whole news item, as the admin editor opens it. */
export function buildNewsItem(overrides: Partial<NewsItem> = {}): NewsItem {
  return {
    ...buildNewsSummary(),
    description: richText('Ya puedes apuntarte.'),
    updatedAt: null,
    ...overrides,
  }
}

/** Learning resource card model read on the site. */
export function buildResourceSummary(
  overrides: Partial<LearningResourceSummary> = {},
): LearningResourceSummary {
  return {
    id: 'resource-1',
    title: 'Guía de Scratch',
    subtitle: 'Primeros pasos',
    type: { id: 'type-article', name: 'Artículo', color: '#123456', isExternal: false },
    url: null,
    createdAt: '2026-02-01T10:00:00Z',
    thumbnailId: 'thumb-r',
    ...overrides,
  }
}

/** Whole learning resource read on the site, as the admin editor opens it. */
export function buildLearningResource(overrides: Partial<LearningResource> = {}): LearningResource {
  return {
    id: 'resource-1',
    title: 'Guía de Python',
    subtitle: 'Primeros pasos',
    type: { id: 'type-article', name: 'Artículo', color: '#123456', isExternal: false },
    url: null,
    createdAt: '2026-03-02T10:00:00Z',
    thumbnailId: 'thumb-resource',
    description: richText('Todo sobre Python.'),
    ...overrides,
  }
}

/** Partner of the admin list with a website and a logo. */
export function buildPartner(overrides: Partial<Partner> = {}): Partner {
  return {
    id: 'partner-1',
    name: 'Acme',
    fromDate: '2024-03-15',
    tier: 1,
    website: 'https://acme.test',
    thumbnailId: 'thumb-partner-1',
    ...overrides,
  }
}

/** Admin table row of the default card event, not featured and without early signup. */
export function buildEventListing(overrides: Partial<EventListing> = {}): EventListing {
  return {
    ...buildEventSummary(),
    featured: false,
    signupStartsAt: '2026-09-01T08:00:00.000Z',
    signupEndsAt: '2026-10-01T20:00:00.000Z',
    earlySignupStartsAt: null,
    ...overrides,
  }
}

/** The default card event as a whole, with one required terms document. */
export function buildEventDetail(overrides: Partial<EventDetail> = {}): EventDetail {
  return {
    id: 'event-1',
    title: 'Día Código Activo',
    subtitle: 'Programa tu futuro',
    description: richText('Un día de código.'),
    startsAt: '2026-10-03',
    endsAt: '2026-10-04',
    signupStartsAt: '2026-09-01T08:00:00.000Z',
    signupEndsAt: '2026-10-01T20:00:00.000Z',
    earlySignupStartsAt: null,
    status: 'signupOpen',
    thumbnailId: 'thumb-1',
    categories: [{ id: 'cat-1', name: 'Robótica', color: '#ff6b5e' }],
    terms: [{ id: 'terms-1', name: 'Normas', required: true, displayOrder: 0 }],
    ...overrides,
  }
}

/** Admin table row of an on-site activity of the default event. */
export function buildActivityListing(overrides: Partial<ActivityListing> = {}): ActivityListing {
  return {
    id: 'act-1',
    title: 'Robotics',
    location: 'Room 1',
    modality: 'On site',
    startsAt: '2026-10-03T09:00:00.000Z',
    endsAt: '2026-10-03T11:00:00.000Z',
    thumbnailId: 'thumb-act',
    ...overrides,
  }
}

/** The default activity as the admin form edits it, wanting four volunteers. */
export function buildActivityDetail(overrides: Partial<ActivityDetail> = {}): ActivityDetail {
  return {
    id: 'act-1',
    title: 'Robotics',
    description: 'Build robots',
    location: 'Room 1',
    modalityId: 'mod-1',
    startsAt: '2026-10-03T09:00:00.000Z',
    endsAt: '2026-10-03T11:00:00.000Z',
    thumbnailId: 'thumb-act',
    roleCapacities: [{ roleTypeId: 'role-1', desiredCount: 4 }],
    ...overrides,
  }
}
