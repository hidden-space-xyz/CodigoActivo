import { useQuery } from '@tanstack/vue-query'

import { partnerQueryKeys } from './query-keys'
import { getSponsorsRequest } from './requests'

/** Public sponsor list; `sponsors` stays `undefined` until the first response. */
export function useSponsors() {
  const query = useQuery({
    queryKey: partnerQueryKeys.sponsors(),
    queryFn: () => getSponsorsRequest(),
  })

  return {
    sponsors: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
  }
}
