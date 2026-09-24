import type {
  EventListItemResponse,
  NewsListItemResponse,
  PartnerResponse,
} from '@/shared/api/generated/models'

import { http, HttpResponse, paged, server } from '../../server'

/** News list item as returned by `GET /api/news`. */
export function buildNewsListItem(overrides: NewsListItemResponse = {}): NewsListItemResponse {
  return {
    id: 'news-item-1',
    title: 'Nueva temporada',
    subtitle: 'Arrancan los talleres',
    createdAt: '2026-09-01T10:00:00Z',
    thumbnailId: '',
    featured: false,
    ...overrides,
  }
}

/** Event list item as returned by `GET /api/events`. */
export function buildEventItem(overrides: EventListItemResponse = {}): EventListItemResponse {
  return {
    id: 'event-1',
    title: 'Hackathon',
    subtitle: 'Un fin de semana programando',
    eventStartsAt: '2030-10-10',
    eventEndsAt: '2030-10-11',
    signupStartsAt: '2030-09-01T00:00:00Z',
    signupEndsAt: '2030-10-01T00:00:00Z',
    thumbnailId: '',
    featured: false,
    categories: [],
    ...overrides,
  }
}

/** Partner as returned by `GET /api/partners`. */
export function buildPartner(overrides: PartnerResponse = {}): PartnerResponse {
  return {
    id: 'partner-1',
    name: 'Acme Labs',
    website: '',
    thumbnailId: '',
    tier: 1,
    ...overrides,
  }
}

/** Data served by `useHomeApi`; every list defaults to empty. */
export interface HomeApiData {
  readonly news?: NewsListItemResponse[]
  readonly featuredEvents?: EventListItemResponse[]
  readonly upcomingEvents?: EventListItemResponse[]
  readonly partners?: PartnerResponse[]
}

/**
 * Registers MSW handlers for every request the home page makes (news items, featured and
 * upcoming events, sponsors) and records their query strings.
 */
export function useHomeApi(data: HomeApiData = {}) {
  const requests: URLSearchParams[] = []
  server.use(
    http.get('/api/news', ({ request }) => {
      requests.push(new URL(request.url).searchParams)
      return HttpResponse.json(paged(data.news ?? []))
    }),
    http.get('/api/events', ({ request }) => {
      const params = new URL(request.url).searchParams
      requests.push(params)
      const items =
        params.get('scope') === 'Upcoming'
          ? (data.upcomingEvents ?? [])
          : (data.featuredEvents ?? [])
      return HttpResponse.json(paged(items))
    }),
    http.get('/api/partners', ({ request }) => {
      requests.push(new URL(request.url).searchParams)
      return HttpResponse.json(paged(data.partners ?? []))
    }),
  )
  return { requests }
}
