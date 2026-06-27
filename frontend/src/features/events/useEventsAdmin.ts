import type { MaybeRefOrGetter } from 'vue'
import { computed, toValue } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  deleteApiEventsEventId,
  getApiEvents,
  getApiEventsEventId,
  postApiEvents,
  putApiEventsEventId,
} from '@/shared/api/generated/endpoints/events/events'
import type { CreateEventRequest, UpdateEventRequest } from '@/shared/api/generated/models'
import { queryKeys } from '@/shared/api/query-keys'

export function useEventsAdmin() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: queryKeys.events })

  const list = useQuery({
    queryKey: queryKeys.events,
    queryFn: ({ signal }) => getApiEvents(undefined, { signal }).then((r) => r.data ?? []),
  })

  const create = useMutation({
    mutationFn: (body: CreateEventRequest) => postApiEvents(body).then((r) => r.data),
    onSuccess: invalidate,
  })

  const update = useMutation({
    mutationFn: (vars: { id: string; body: UpdateEventRequest }) =>
      putApiEventsEventId(vars.id, vars.body).then((r) => r.data),
    onSuccess: invalidate,
  })

  const remove = useMutation({
    mutationFn: (id: string) => deleteApiEventsEventId(id),
    onSuccess: invalidate,
  })

  return { list, create, update, remove }
}

export function useEvent(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => queryKeys.event(toValue(eventId))),
    queryFn: ({ signal }) => getApiEventsEventId(toValue(eventId), { signal }).then((r) => r.data),
  })
}
