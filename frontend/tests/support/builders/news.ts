import type { NewsItemResponse, NewsListItemResponse } from '@/shared/api/generated/models'

import { AUDIT, richText } from './common'

/** News list item as returned by `GET /api/news`. */
export function buildNewsListItem(
  overrides: Partial<NewsListItemResponse> = {},
): NewsListItemResponse {
  return {
    id: 'news-item-1',
    title: 'Abrimos inscripciones',
    subtitle: 'Nueva temporada',
    ...AUDIT,
    createdAt: '2026-02-01T10:00:00Z',
    updatedAt: null,
    thumbnailId: 'thumb-news-item',
    featured: false,
    ...overrides,
  }
}

/** Full news item as returned by `GET /api/news/:id`. */
export function buildNewsItemResponse(overrides: Partial<NewsItemResponse> = {}): NewsItemResponse {
  return {
    ...buildNewsListItem(),
    description: richText('Ya puedes apuntarte.'),
    updatedAt: '2026-02-03T10:00:00Z',
    ...overrides,
  }
}
