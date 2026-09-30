import { queryOptions } from '@tanstack/vue-query'

import type { ServerTableSource } from '@/shared/lib/paging'

import type { EventCategory, EventCategoryListParams } from '../model/types'
import { getEventCategoriesRequest, getEventCategoryListPageRequest } from './requests'

/** Query keys of event categories; `all` covers the options and the admin list. */
export const eventCategoryKeys = {
  all: ['event-categories'] as const,
  options: () => [...eventCategoryKeys.all, 'options'] as const,
  list: () => [...eventCategoryKeys.all, 'list'] as const,
}

/** Query options of event categories. */
export const eventCategoryQueries = {
  /** Up to 100 categories for selectors and filters. */
  options: () =>
    queryOptions({
      queryKey: eventCategoryKeys.options(),
      queryFn: () => getEventCategoriesRequest(),
    }),
}

/** Admin category list, paged, filtered and sorted by the API. */
export const eventCategoryList: ServerTableSource<EventCategory, EventCategoryListParams> = {
  queryKey: eventCategoryKeys.list(),
  fetchPage: (params) => getEventCategoryListPageRequest(params),
}
