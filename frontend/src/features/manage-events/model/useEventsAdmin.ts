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
import type {
  CreateEventRequest,
  EventResponse,
  GetApiEventsParams,
  UpdateEventRequest,
} from '@/shared/api/generated/models'
import { toPage, unwrapOrNull } from '@/shared/api'
import { useServerTable } from '@/shared/lib'
import { eventQueryKeys } from '@/entities/event'
import { deleteThumbnail } from '@/entities/file'

export function useEventsAdmin() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: eventQueryKeys.all })

  const table = useServerTable<EventResponse, GetApiEventsParams>({
    queryKey: [...eventQueryKeys.all, 'admin'],
    fetchPage: (params) => getApiEvents(params).then(toPage),
    defaultSort: { field: 'eventStartsAt', order: 1 },
    columns: {
      title: { type: 'text' },
      subtitle: { type: 'text' },
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
    mutationFn: (vars: { id: string; thumbnailId?: string | null | undefined }) =>
      deleteApiEventsEventId(vars.id),
    // The backend does not cascade to the thumbnail file; removing it here keeps the rule with
    // the mutation instead of in every delete-confirmation callback.
    onSuccess: (_data, vars) => {
      void deleteThumbnail(vars.thumbnailId)
      return invalidate()
    },
  })

  const feature = useMutation({
    mutationFn: (id: string) => patchApiEventsEventIdFeature(id).then((r) => r.data),
    onSuccess: invalidate,
  })

  return { table, create, update, remove, feature }
}

export function useEvent(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => eventQueryKeys.adminDetail(toValue(eventId))),
    queryFn: () => unwrapOrNull<EventResponse>(getApiEventsEventId(toValue(eventId))),
  })
}
