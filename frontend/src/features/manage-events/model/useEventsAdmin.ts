import type { MaybeRefOrGetter } from 'vue'
import { computed, toValue } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  deleteApiEventsEventId,
  getApiEvents,
  getApiEventsEventId,
  patchApiEventsEventIdFeature,
  postApiEvents,
  putApiEventsEventId,
} from '@/shared/api/generated/endpoints/events/events'
import type { CreateEventRequest, UpdateEventRequest } from '@/shared/api/generated/models'
import { eventQueryKeys } from '@/entities/event'

export function useEventsAdmin() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: eventQueryKeys.all })

  const list = useQuery({
    queryKey: eventQueryKeys.all,
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

  const feature = useMutation({
    mutationFn: (id: string) => patchApiEventsEventIdFeature(id).then((r) => r.data),
    onSuccess: invalidate,
  })

  return { list, create, update, remove, feature }
}

export function useEvent(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => eventQueryKeys.detail(toValue(eventId))),
    queryFn: ({ signal }) => getApiEventsEventId(toValue(eventId), { signal }).then((r) => r.data),
  })
}
