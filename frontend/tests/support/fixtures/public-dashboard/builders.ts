import type {
  ActivityResponse,
  AnnouncementListItemResponse,
  AnnouncementResponse,
  DashboardAnalyticsResponse,
  EventListItemResponse,
  EventResponse,
  ResourceListItemResponse,
  ResourceResponse,
} from '@/shared/api/generated/models'
import type { ChartPalette } from '@/shared/lib'

/** Copy of `value` without `keys`, for API payloads where a field is absent rather than undefined. */
export function omit<T extends object, K extends keyof T>(value: T, ...keys: K[]): Omit<T, K> {
  const copy = { ...value }
  for (const key of keys) delete copy[key]
  return copy
}

/** Far-away timestamps so event status does not depend on the day the suite runs. */
export const LONG_AGO = '2020-01-01T09:00:00Z'
export const FAR_FUTURE = '2099-06-01T09:00:00Z'

/** Event whose general signup window is open right now. */
export function buildEventResponse(overrides: EventResponse = {}): EventResponse {
  return {
    id: 'event-1',
    title: 'Hackathon de primavera',
    subtitle: 'Programa tu futuro',
    description: 'Un fin de semana de código.',
    eventStartsAt: '2099-06-10T09:00:00Z',
    eventEndsAt: '2099-06-11T18:00:00Z',
    earlySignupStartsAt: null,
    signupStartsAt: LONG_AGO,
    signupEndsAt: FAR_FUTURE,
    thumbnailId: 'thumb-event',
    featured: false,
    categories: [{ categoryTypeId: 'cat-1', name: 'Programación', color: '#ff6600' }],
    ...overrides,
  }
}

/** Upcoming or past event list item. */
export function buildEventListItem(overrides: EventListItemResponse = {}): EventListItemResponse {
  return {
    id: 'event-1',
    title: 'Hackathon de primavera',
    subtitle: 'Programa tu futuro',
    eventStartsAt: '2099-06-10T09:00:00Z',
    eventEndsAt: '2099-06-11T18:00:00Z',
    signupStartsAt: LONG_AGO,
    signupEndsAt: FAR_FUTURE,
    thumbnailId: '',
    categories: [],
    ...overrides,
  }
}

/** Public activity of `event-1`. */
export function buildActivityResponse(overrides: ActivityResponse = {}): ActivityResponse {
  return {
    id: 'activity-1',
    title: 'Taller de robótica',
    description: 'Construye tu primer robot',
    location: 'Aula 3',
    modalityName: 'Presencial',
    activityStartsAt: '2099-06-10T09:00:00Z',
    activityEndsAt: '2099-06-10T11:00:00Z',
    eventId: 'event-1',
    roleCapacities: [],
    ...overrides,
  }
}

/** Resource list item without external URL. */
export function buildResourceListItem(
  overrides: ResourceListItemResponse = {},
): ResourceListItemResponse {
  return {
    id: 'resource-1',
    title: 'Guía de Python',
    subtitle: 'Primeros pasos',
    url: null,
    createdAt: '2026-03-02T10:00:00Z',
    thumbnailId: '',
    ...overrides,
  }
}

/** Full resource without external URL. */
export function buildResourceResponse(overrides: ResourceResponse = {}): ResourceResponse {
  return {
    ...buildResourceListItem(),
    description: 'Todo sobre Python.',
    thumbnailId: 'thumb-resource',
    ...overrides,
  }
}

/** Announcement list item. */
export function buildAnnouncementListItem(
  overrides: AnnouncementListItemResponse = {},
): AnnouncementListItemResponse {
  return {
    id: 'announcement-1',
    title: 'Abrimos inscripciones',
    subtitle: 'Nueva temporada',
    createdAt: '2026-02-01T10:00:00Z',
    thumbnailId: '',
    featured: false,
    ...overrides,
  }
}

/** Full announcement. */
export function buildAnnouncementResponse(
  overrides: AnnouncementResponse = {},
): AnnouncementResponse {
  return {
    ...buildAnnouncementListItem(),
    description: 'Ya puedes apuntarte.',
    updatedAt: '2026-02-03T10:00:00Z',
    thumbnailId: 'thumb-announcement',
    ...overrides,
  }
}

/** Distinct, recognizable colors so assertions can tell which palette entry was used. */
export const TEST_PALETTE: ChartPalette = {
  orange: 'orange',
  orangeSoft: 'orange-soft',
  lime: 'lime',
  limeSoft: 'lime-soft',
  azure: 'azure',
  azureSoft: 'azure-soft',
  success: 'success',
  successSoft: 'success-soft',
  warning: 'warning',
  warningSoft: 'warning-soft',
  danger: 'danger',
  dangerSoft: 'danger-soft',
  text: 'text',
  textMuted: 'text-muted',
  textDim: 'text-dim',
  grid: 'grid',
  surface: 'surface',
  border: 'border',
}

/** Dashboard analytics with data in every chart. */
export function buildDashboardAnalytics(
  overrides: DashboardAnalyticsResponse = {},
): DashboardAnalyticsResponse {
  return {
    rangeStart: '2025-09-17',
    rangeEnd: '2026-09-17',
    granularity: 'month',
    kpis: [
      { key: 'users', total: 1200, inRange: 30, previousRange: 20 },
      { key: 'members', total: 80, inRange: 2, previousRange: 4 },
      { key: 'inscriptions', total: 500, inRange: 10, previousRange: 10 },
      { key: 'events', total: 12, inRange: 0, previousRange: 0 },
    ],
    userGrowth: {
      buckets: ['2026-08-01', '2026-09-01'],
      series: [{ key: 'member', values: [1, 2] }],
    },
    inscriptions: {
      buckets: ['2026-08-01', '2026-09-01'],
      series: [{ key: 'confirmed', values: [3, 4] }],
    },
    contentPublished: {
      buckets: ['2026-08-01', '2026-09-01'],
      series: [{ key: 'resources', values: [0, 1] }],
    },
    eventsCalendar: {
      buckets: ['2026-08-01', '2026-09-01'],
      series: [{ key: 'past', values: [1, 0] }],
    },
    usersByType: [{ key: 'member', count: 10 }],
    audienceComposition: [{ key: 'adults', count: 7 }],
    participantsByGender: [{ key: 'Female', count: 5 }],
    eventsByCategory: [{ key: 'cat-1', label: 'Programación', color: '#ff6600', count: 3 }],
    topEvents: [{ eventId: 'event-1', title: 'Hackathon de primavera', confirmed: 42 }],
    occupancy: {
      confirmed: 30,
      desired: 40,
      events: [{ eventId: 'event-1', title: 'Hackathon de primavera', confirmed: 30, desired: 40 }],
    },
    ...overrides,
  }
}
