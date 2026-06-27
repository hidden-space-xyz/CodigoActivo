import { useQuery } from '@tanstack/vue-query'

import { getEventBoard } from '@/modules/events/application/use-cases/get-event-board.use-case'
import { eventRepository } from '@/modules/events/infrastructure/repositories/event-repository.provider'

import { eventQueryKeys } from './event-query-keys'

export function useEventBoard() {
  const query = useQuery({
    queryKey: eventQueryKeys.board(),
    queryFn: () => getEventBoard(eventRepository),
  })

  return {
    boardEvents: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
  }
}
