import { useQuery } from '@tanstack/vue-query'

import { getSponsors } from '@/modules/home/application/use-cases/get-sponsors.use-case'
import { sponsorRepository } from '@/modules/home/infrastructure/repositories/sponsor-repository.provider'

export function useSponsors() {
  const query = useQuery({
    queryKey: ['sponsors'] as const,
    queryFn: () => getSponsors(sponsorRepository),
  })

  return {
    sponsors: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
  }
}
