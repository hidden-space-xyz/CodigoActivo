import type { MaybeRefOrGetter } from 'vue'
import { computed, toValue } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import type {
  CreateEventRequest,
  EventListItemResponse,
  GetApiEventsParams,
  UpdateEventRequest,
} from '@/shared/api/generated/models'
import { useServerTable } from '@/shared/lib'
import {
  createEventRequest,
  deleteEventRequest,
  eventQueryKeys,
  getEventAdminRequest,
  getEventsAdminPageRequest,
  toggleEventFeatureRequest,
  updateEventRequest,
} from '@/entities/event'

export function useEventsAdmin() {
  const queryClient = useQueryClient()
  const invalidate = () => queryClient.invalidateQueries({ queryKey: eventQueryKeys.all })

  const table = useServerTable<EventListItemResponse, GetApiEventsParams>({
    queryKey: eventQueryKeys.adminTable(),
    fetchPage: (params) => getEventsAdminPageRequest(params),
    defaultSort: { field: 'eventStartsAt', order: 1 },
    columns: {
      title: { type: 'text' },
      subtitle: { type: 'text' },
      category: { param: 'categoryTypeId' },
      eventDate: { type: 'dateRange', fromParam: 'eventDateFrom', toParam: 'eventDateTo' },
      signup: { type: 'dateRange', fromParam: 'signupFrom', toParam: 'signupTo' },
    },
  })

  const create = useMutation({
    mutationFn: (body: CreateEventRequest) => createEventRequest(body),
    onSuccess: invalidate,
  })

  const update = useMutation({
    mutationFn: (vars: { id: string; body: UpdateEventRequest }) =>
      updateEventRequest(vars.id, vars.body),
    onSuccess: invalidate,
  })

  const remove = useMutation({
    mutationFn: (id: string) => deleteEventRequest(id),
    onSuccess: invalidate,
  })

  const feature = useMutation({
    mutationFn: (id: string) => toggleEventFeatureRequest(id),
    onSuccess: invalidate,
  })

  const fetchOne = (id: string) => getEventAdminRequest(id)

  return { table, create, update, remove, feature, fetchOne }
}

export function useEvent(eventId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => eventQueryKeys.adminDetail(toValue(eventId))),
    queryFn: () => getEventAdminRequest(toValue(eventId)),
  })
}
