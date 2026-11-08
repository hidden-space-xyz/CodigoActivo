import { queryOptions } from '@tanstack/vue-query'

import { getLegalEntityRequest } from './requests'

/** Query keys of the legal identity. */
const legalEntityKeys = {
  all: ['legal-entity'] as const,
}

/** Query options of the legal identity. */
export const legalEntityQueries = {
  /** The association's legal identity and contact details, as the deployment configures them. */
  detail: () =>
    queryOptions({
      queryKey: legalEntityKeys.all,
      queryFn: () => getLegalEntityRequest(),
    }),
}
