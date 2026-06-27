import { computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { getHomeEvents } from '@/modules/events/application/use-cases/get-home-events.use-case'
import { eventRepository } from '@/modules/events/infrastructure/repositories/event-repository.provider'

import { eventQueryKeys } from './event-query-keys'

export function useHomeEvents() {
  const query = useQuery({
    queryKey: eventQueryKeys.board(),
    queryFn: () => getHomeEvents(eventRepository),
  })

  return {
    featured: computed(() => query.data.value?.featured ?? null),
    items: computed(() => query.data.value?.items ?? []),
    isLoading: query.isLoading,
    isError: query.isError,
  }
}
