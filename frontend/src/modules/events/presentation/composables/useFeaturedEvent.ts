import { useQuery } from '@tanstack/vue-query'

import { getFeaturedEvent } from '@/modules/events/application/use-cases/get-featured-event.use-case'
import { eventRepository } from '@/modules/events/infrastructure/repositories/event-repository.provider'

import { eventQueryKeys } from './event-query-keys'

export function useFeaturedEvent() {
  const query = useQuery({
    queryKey: eventQueryKeys.featured(),
    queryFn: () => getFeaturedEvent(eventRepository),
  })

  return {
    featuredEvent: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
  }
}
