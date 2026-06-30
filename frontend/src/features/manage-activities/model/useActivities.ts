import type { MaybeRefOrGetter } from 'vue'
import { computed, toValue } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  deleteApiActivitiesActivityId,
  getApiActivitiesEventId,
  getApiActivitiesEventIdActivityId,
  postApiActivitiesEventId,
  putApiActivitiesActivityId,
} from '@/shared/api/generated/endpoints/activities/activities'
import type { CreateActivityRequest, UpdateActivityRequest } from '@/shared/api/generated/models'

export function useActivities(eventId: MaybeRefOrGetter<string>) {
  const queryClient = useQueryClient()
  const queryKey = computed(() => ['activities', 'event', toValue(eventId)] as const)
  const invalidate = () => queryClient.invalidateQueries({ queryKey: queryKey.value })

  const list = useQuery({
    queryKey,
    queryFn: ({ signal }) =>
      getApiActivitiesEventId(toValue(eventId), { signal }).then((r) => r.data ?? []),
  })

  const create = useMutation({
    mutationFn: (body: CreateActivityRequest) =>
      postApiActivitiesEventId(toValue(eventId), body).then((r) => r.data),
    onSuccess: invalidate,
  })

  const update = useMutation({
    mutationFn: (vars: { id: string; body: UpdateActivityRequest }) =>
      putApiActivitiesActivityId(vars.id, vars.body).then((r) => r.data),
    onSuccess: invalidate,
  })

  const remove = useMutation({
    mutationFn: (id: string) => deleteApiActivitiesActivityId(id),
    onSuccess: invalidate,
  })

  function fetchOne(eventIdValue: string, activityId: string) {
    return getApiActivitiesEventIdActivityId(eventIdValue, activityId).then((r) => r.data)
  }

  return { list, create, update, remove, fetchOne }
}
