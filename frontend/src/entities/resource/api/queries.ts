import { queryOptions } from '@tanstack/vue-query'

import type { PagedListSource, ServerTableSource } from '@/shared/lib/paging'

import type { LearningResourceSummary, ResourceListParams } from '../model/types'
import {
  getResourceListPageRequest,
  getResourceRequest,
  getResourcesPageRequest,
  getResourceTypesRequest,
} from './requests'

/**
 * Query keys of resources; `all` covers the resources themselves. Their types live apart, since
 * no change to a resource alters them.
 */
export const resourceKeys = {
  all: ['resources'] as const,
  pages: (search: string) => [...resourceKeys.all, 'pages', search] as const,
  detail: (id: string) => [...resourceKeys.all, 'detail', id] as const,
  list: () => [...resourceKeys.all, 'list'] as const,
  types: () => ['resource-types'] as const,
}

/** Query options of resources. */
export const resourceQueries = {
  /** A whole resource, or `null` when it does not exist. */
  detail: (id: string) =>
    queryOptions({
      queryKey: resourceKeys.detail(id),
      queryFn: () => getResourceRequest(id),
    }),
  /** Every resource type, for selectors and filters. */
  types: () =>
    queryOptions({
      queryKey: resourceKeys.types(),
      queryFn: () => getResourceTypesRequest(),
    }),
}

/** Public resources matching `search`, newest first, loaded page by page. */
export function resourcePages(search: string): PagedListSource<LearningResourceSummary> {
  return {
    queryKey: resourceKeys.pages(search),
    fetchPage: (page, pageSize) => getResourcesPageRequest(search, page, pageSize),
  }
}

/** Admin resource list, paged, filtered and sorted by the API. */
export const resourceList: ServerTableSource<LearningResourceSummary, ResourceListParams> = {
  queryKey: resourceKeys.list(),
  fetchPage: (params) => getResourceListPageRequest(params),
}
