import type { MaybeRefOrGetter } from 'vue'
import { computed, toValue } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  deleteApiEventsEventId,
  patchApiEventsEventIdFeature,
  postApiEvents,
  putApiEventsEventId,
} from '@/shared/api/generated/endpoints/events/events'
import type {
  CreateEventRequest,
  EventResponse,
  UpdateEventRequest,
} from '@/shared/api/generated/models'
import { fetchODataEntity } from '@/shared/api'
import { useODataTable } from '@/shared/lib'
import { eventQueryKeys } from '@/entities/event'

export function useEventsAdmin() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: eventQueryKeys.all })

  const table = useODataTable<EventResponse>({
    resource: 'Events',
    queryKey: [...eventQueryKeys.all, 'admin'],
    defaultSort: { field: 'eventStartsAt', order: 1 },
    columns: {
      title: { type: 'text' },
      subtitle: { type: 'text' },
      eventStartsAt: { type: 'date' },
      eventEndsAt: { type: 'date' },
      featured: { type: 'boolean' },
    },
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

  return { table, create, update, remove, feature }
}

export function useEvent(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => eventQueryKeys.detail(toValue(eventId))),
    queryFn: () => fetchODataEntity<EventResponse>('Events', toValue(eventId)),
  })
}
