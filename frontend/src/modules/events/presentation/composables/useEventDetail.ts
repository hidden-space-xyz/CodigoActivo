import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { getEventById } from '@/modules/events/application/use-cases/get-event-by-id.use-case'
import { eventRepository } from '@/modules/events/infrastructure/repositories/event-repository.provider'

import { eventQueryKeys } from './event-query-keys'

export function useEventDetail(eventId: MaybeRefOrGetter<string>) {
  const id = computed(() => toValue(eventId))

  const query = useQuery({
    queryKey: computed(() => eventQueryKeys.detail(id.value)),
    queryFn: () => getEventById(eventRepository, id.value),
  })

  const notFound = computed(() => !query.isLoading.value && query.data.value === null)

  return {
    event: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    notFound,
  }
}
