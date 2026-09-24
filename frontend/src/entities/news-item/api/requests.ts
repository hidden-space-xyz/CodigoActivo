import {
  deleteApiNewsNewsItemId,
  getApiNews,
  getApiNewsNewsItemId,
  getApiNewsYears,
  patchApiNewsNewsItemIdFeature,
  postApiNews,
  putApiNewsNewsItemId,
} from '@/shared/api/generated/endpoints/news/news'
import type {
  CreateNewsItemRequest,
  GetApiNewsParams,
  NewsItemResponse,
  NewsListItemResponse,
  UpdateNewsItemRequest,
} from '@/shared/api/generated/models'
import { FEATURED_FIRST_SORT, toPage, unwrapOrNull } from '@/shared/api'
import type { PagedListPage } from '@/shared/lib'

import type { HomeNews, NewsItem, NewsSummary } from '../model/types'
import { toNewsItem, toNewsSummary } from './mapper'

/** Lists the years that have news items, as strings for the year selector. */
export async function getNewsYearsRequest(): Promise<readonly string[]> {
  const { data } = await getApiNewsYears()
  return (data ?? []).map(String)
}

/** Fetches one page of a year's news items, newest first; an empty `search` is not sent. */
export async function getNewsByYearPageRequest(
  year: string,
  search: string,
  page: number,
  pageSize: number,
): Promise<PagedListPage<NewsSummary>> {
  const result = await getApiNews({
    year: Number(year),
    ...(search ? { search } : {}),
    sort: '-createdAt',
    page,
    pageSize,
  })
  const { items, total } = toPage(result)
  return { items: items.map(toNewsSummary), total }
}

/** Fetches four news items, featured first, and splits off the first one as the highlight. */
export async function getHomeNewsRequest(): Promise<HomeNews> {
  const { data } = await getApiNews({ sort: FEATURED_FIRST_SORT, pageSize: 4 })
  const [featured = null, ...items] = (data.items ?? []).map(toNewsSummary)
  return { featured, items }
}

/** Loads a news item for the public detail page; resolves `null` on 404. */
export async function getNewsItemByIdRequest(id: string): Promise<NewsItem | null> {
  const response = await unwrapOrNull<NewsItemResponse>(getApiNewsNewsItemId(id))
  return response ? toNewsItem(response) : null
}

/** Fetches one page of unmapped news items for the admin table (server-side filters/sort). */
export function getNewsAdminPageRequest(
  params: GetApiNewsParams,
): Promise<{ items: NewsListItemResponse[]; total: number }> {
  return getApiNews(params).then(toPage)
}

/** Loads the unmapped news item for the admin edit form; resolves `null` on 404. */
export function getNewsItemAdminRequest(id: string) {
  return unwrapOrNull<NewsItemResponse>(getApiNewsNewsItemId(id))
}

/** Creates a news item; the response's `data` holds the created item. */
export function createNewsItemRequest(body: CreateNewsItemRequest) {
  return postApiNews(body)
}

/** Replaces a news item's content (`PUT`). */
export function updateNewsItemRequest(id: string, body: UpdateNewsItemRequest) {
  return putApiNewsNewsItemId(id, body)
}

/** Deletes a news item from the admin panel. */
export function deleteNewsItemRequest(id: string) {
  return deleteApiNewsNewsItemId(id)
}

/**
 * Marks the news item as the featured one. Despite the name this is not a toggle: the API
 * features this news item and unfeatures every other one.
 */
export function toggleNewsItemFeatureRequest(id: string) {
  return patchApiNewsNewsItemIdFeature(id)
}
