import type { MaybeRefOrGetter } from 'vue'
import { computed, toValue } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  deleteApiActivitiesActivityId,
  postApiActivitiesEventId,
  putApiActivitiesActivityId,
} from '@/shared/api/generated/endpoints/activities/activities'
import type { CreateActivityRequest, UpdateActivityRequest } from '@/shared/api/generated/models'
import {
  activityQueryKeys,
  getActivityByIdRequest,
  listEventActivitiesRequest,
} from '@/entities/activity'

export function useActivities(eventId: MaybeRefOrGetter<string>) {
  const queryClient = useQueryClient()
  const queryKey = computed(() => activityQueryKeys.adminByEvent(toValue(eventId)))
  // Creating/removing activities also changes the event summary report's counts, and the public
  // event page caches the same activities under the entity's public key.
  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: queryKey.value })
    void queryClient.invalidateQueries({
      queryKey: ['reports', 'event-summary', toValue(eventId)],
    })
    void queryClient.invalidateQueries({
      queryKey: activityQueryKeys.publicByEvent(toValue(eventId)),
    })
  }

  const list = useQuery({
    queryKey,
    queryFn: () => listEventActivitiesRequest(toValue(eventId)),
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
    // The backend cascades orphaned thumbnail files, so no client-side file cleanup is needed.
    mutationFn: (id: string) => deleteApiActivitiesActivityId(id),
    onSuccess: invalidate,
  })

  function fetchOne(activityId: string) {
    return getActivityByIdRequest(activityId)
  }

  return { list, create, update, remove, fetchOne }
}
