import type {
  CreateNewsItemRequest,
  NewsItemResponse,
  NewsListItemResponse,
} from '@/shared/api/generated/models'

import type { NewsItem, NewsItemInput, NewsSummary } from '../model/types'

/** Maps a news item of a list. */
export function toNewsSummary(response: NewsListItemResponse): NewsSummary {
  return {
    id: response.id,
    title: response.title,
    subtitle: response.subtitle,
    createdAt: response.createdAt,
    thumbnailId: response.thumbnailId,
    featured: response.featured,
  }
}

/** Maps a whole news item; one never edited has no `updatedAt`. */
export function toNewsItem(response: NewsItemResponse): NewsItem {
  return {
    ...toNewsSummary(response),
    description: response.description,
    updatedAt: response.updatedAt ?? null,
  }
}

/** Builds the body that creates or replaces a news item; both endpoints take the same fields. */
export function toNewsItemRequest(input: NewsItemInput): CreateNewsItemRequest {
  return {
    title: input.title,
    subtitle: input.subtitle,
    description: input.description,
    thumbnailId: input.thumbnailId,
  }
}
