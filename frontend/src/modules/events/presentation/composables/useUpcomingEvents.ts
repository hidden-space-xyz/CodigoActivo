import { useQuery } from '@tanstack/vue-query'

import { getUpcomingEvents } from '@/modules/events/application/use-cases/get-upcoming-events.use-case'
import { eventRepository } from '@/modules/events/infrastructure/repositories/event-repository.provider'

import { eventQueryKeys } from './event-query-keys'

export function useUpcomingEvents() {
  const query = useQuery({
    queryKey: eventQueryKeys.upcoming(),
    queryFn: () => getUpcomingEvents(eventRepository),
  })

  return {
    upcomingEvents: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
  }
}
