import {
  deleteApiNewsNewsItemId,
  getApiNews,
  getApiNewsNewsItemId,
  getApiNewsYears,
  patchApiNewsNewsItemIdFeature,
  postApiNews,
  putApiNewsNewsItemId,
} from '@/shared/api/generated/endpoints/news/news'
import { FEATURED_FIRST_SORT, toPage, unwrapOrNull } from '@/shared/api'
import type { PagedListPage, ServerTablePage } from '@/shared/lib/paging'

import type { HomeNews, NewsItem, NewsItemInput, NewsListParams, NewsSummary } from '../model/types'
import { toNewsItem, toNewsItemRequest, toNewsSummary } from './mapper'

/** Lists the years that have news items, as strings for the year selector. */
export async function getNewsYearsRequest(): Promise<readonly string[]> {
  const { data } = await getApiNewsYears()
  return data.map(String)
}

/** Fetches one page of a year's news items, newest first; an empty `search` is not sent. */
export async function getNewsByYearPageRequest(
  year: string,
  search: string,
  page: number,
  pageSize: number,
): Promise<PagedListPage<NewsSummary>> {
  const response = await getApiNews({
    year: Number(year),
    ...(search ? { search } : {}),
    sort: '-createdAt',
    page,
    pageSize,
  })
  const { items, total } = toPage(response)
  return { items: items.map(toNewsSummary), total }
}

/** Fetches four news items, featured first, and splits off the first one as the highlight. */
export async function getHomeNewsRequest(): Promise<HomeNews> {
  const response = await getApiNews({ sort: FEATURED_FIRST_SORT, pageSize: 4 })
  const [featured = null, ...items] = toPage(response).items.map(toNewsSummary)
  return { featured, items }
}

/** Loads a whole news item; resolves to `null` when it does not exist (404). */
export async function getNewsItemRequest(id: string): Promise<NewsItem | null> {
  const response = await unwrapOrNull(getApiNewsNewsItemId(id))
  return response ? toNewsItem(response) : null
}

/** Fetches one page of the admin news list. */
export async function getNewsListPageRequest(
  params: NewsListParams,
): Promise<ServerTablePage<NewsSummary>> {
  const { items, total } = toPage(await getApiNews(params))
  return { items: items.map(toNewsSummary), total }
}

/** Creates a news item. */
export async function createNewsItemRequest(input: NewsItemInput): Promise<void> {
  await postApiNews(toNewsItemRequest(input))
}

/** Replaces a news item. */
export async function updateNewsItemRequest(id: string, input: NewsItemInput): Promise<void> {
  await putApiNewsNewsItemId(id, toNewsItemRequest(input))
}

/** Deletes a news item. */
export async function deleteNewsItemRequest(id: string): Promise<void> {
  await deleteApiNewsNewsItemId(id)
}

/** Makes the news item the featured one; the API unfeatures every other news item. */
export async function featureNewsItemRequest(id: string): Promise<void> {
  await patchApiNewsNewsItemIdFeature(id)
}
