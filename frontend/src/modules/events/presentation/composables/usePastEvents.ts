import { computed, ref } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { getPastEvents } from '@/modules/events/application/use-cases/get-past-events.use-case'
import {
  ALL_YEARS,
  availableYears,
  selectEventsByYear,
} from '@/modules/events/domain/services/filter-events'
import { eventRepository } from '@/modules/events/infrastructure/repositories/event-repository.provider'

import { eventQueryKeys } from './event-query-keys'

export function usePastEvents() {
  const query = useQuery({
    queryKey: eventQueryKeys.past(),
    queryFn: () => getPastEvents(eventRepository),
  })

  const selectedYear = ref<string>(ALL_YEARS)

  const years = computed(() => availableYears(query.data.value ?? []))
  const filteredEvents = computed(() =>
    selectEventsByYear(query.data.value ?? [], selectedYear.value),
  )

  function setYear(year: string): void {
    selectedYear.value = year
  }

  return {
    isLoading: query.isLoading,
    isError: query.isError,
    years,
    filteredEvents,
    selectedYear,
    setYear,
    ALL_YEARS,
  }
}
