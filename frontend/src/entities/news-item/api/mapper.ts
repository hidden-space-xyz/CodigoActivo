import type { NewsItemResponse, NewsListItemResponse } from '@/shared/api/generated/models'
import { formatDate } from '@/shared/lib'

import type { NewsItem, NewsSummary } from '../model/types'

/** Maps a list item for cards, formatting `createdAt` into the display `date`. */
export function toNewsSummary(response: NewsListItemResponse): NewsSummary {
  return {
    id: response.id ?? '',
    title: response.title ?? '',
    subtitle: response.subtitle ?? '',
    date: response.createdAt ? formatDate(response.createdAt) : '',
    thumbnailId: response.thumbnailId ?? '',
    featured: response.featured ?? false,
  }
}

/** Maps the full news item for the detail page, keeping the raw ISO timestamps as well. */
export function toNewsItem(response: NewsItemResponse): NewsItem {
  return {
    ...toNewsSummary(response),
    description: response.description ?? '',
    publishedAt: response.createdAt ?? null,
    updatedAt: response.updatedAt ?? null,
  }
}
