import { queryOptions } from '@tanstack/vue-query'

import type { PagedListSource, ServerTableSource } from '@/shared/lib/paging'

import type { NewsListParams, NewsSummary } from '../model/types'
import {
  getHomeNewsRequest,
  getNewsByYearPageRequest,
  getNewsItemRequest,
  getNewsListPageRequest,
  getNewsYearsRequest,
} from './requests'

/** Query keys of news; `all` covers every one of them. */
export const newsKeys = {
  all: ['news'] as const,
  years: () => [...newsKeys.all, 'years'] as const,
  pages: (year: string, search: string) => [...newsKeys.all, 'pages', year, search] as const,
  home: () => [...newsKeys.all, 'home'] as const,
  detail: (id: string) => [...newsKeys.all, 'detail', id] as const,
  list: () => [...newsKeys.all, 'list'] as const,
}

/** Query options of news. */
export const newsQueries = {
  /** Years that have news items, newest first. */
  years: () =>
    queryOptions({
      queryKey: newsKeys.years(),
      queryFn: () => getNewsYearsRequest(),
    }),
  /** Home page block: up to four news items, featured first. */
  home: () =>
    queryOptions({
      queryKey: newsKeys.home(),
      queryFn: () => getHomeNewsRequest(),
    }),
  /** A whole news item, or `null` when it does not exist. */
  detail: (id: string) =>
    queryOptions({
      queryKey: newsKeys.detail(id),
      queryFn: () => getNewsItemRequest(id),
    }),
}

/** News items of `year` matching `search`, newest first, loaded page by page. */
export function newsPages(year: string, search: string): PagedListSource<NewsSummary> {
  return {
    queryKey: newsKeys.pages(year, search),
    fetchPage: (page, pageSize) => getNewsByYearPageRequest(year, search, page, pageSize),
  }
}

/** Admin news list, paged, filtered and sorted by the API. */
export const newsList: ServerTableSource<NewsSummary, NewsListParams> = {
  queryKey: newsKeys.list(),
  fetchPage: (params) => getNewsListPageRequest(params),
}
