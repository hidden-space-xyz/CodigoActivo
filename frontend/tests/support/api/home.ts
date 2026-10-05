import type {
  EventListItemResponse,
  NewsListItemResponse,
  PartnerResponse,
} from '@/shared/api/generated/models'

import { http, HttpResponse, paged, server } from '../server'

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
        params.get('featured') === 'true'
          ? (data.featuredEvents ?? [])
          : (data.upcomingEvents ?? [])
      return HttpResponse.json(paged(items))
    }),
    http.get('/api/partners', ({ request }) => {
      requests.push(new URL(request.url).searchParams)
      return HttpResponse.json(paged(data.partners ?? []))
    }),
  )
  return { requests }
}
