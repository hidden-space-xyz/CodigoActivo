import { queryOptions } from '@tanstack/vue-query'

import type { ServerTableSource } from '@/shared/lib/paging'

import type { Partner, PartnerListParams } from '../model/types'
import { getPartnersPageRequest, getSponsorsRequest } from './requests'

/** Query keys of partners; `all` covers the public sponsors and the admin list. */
export const partnerKeys = {
  all: ['partners'] as const,
  sponsors: () => [...partnerKeys.all, 'sponsors'] as const,
  list: () => [...partnerKeys.all, 'list'] as const,
}

/** Query options of partners. */
export const partnerQueries = {
  /** Partners shown as sponsors on the home page. */
  sponsors: () =>
    queryOptions({
      queryKey: partnerKeys.sponsors(),
      queryFn: () => getSponsorsRequest(),
    }),
}

/** Admin partner list, paged, filtered and sorted by the API. */
export const partnerList: ServerTableSource<Partner, PartnerListParams> = {
  queryKey: partnerKeys.list(),
  fetchPage: (params) => getPartnersPageRequest(params),
}
