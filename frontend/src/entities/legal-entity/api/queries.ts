import { queryOptions } from '@tanstack/vue-query'

import { getLegalEntityRequest } from './requests'

/** Query keys of the legal identity. */
const legalEntityKeys = {
  all: ['legal-entity'] as const,
}

/** Query options of the legal identity. */
export const legalEntityQueries = {
  /** The data controller the privacy policy and the registration notice name. */
  detail: () =>
    queryOptions({
      queryKey: legalEntityKeys.all,
      queryFn: () => getLegalEntityRequest(),
    }),
}
