import type { MaybeRefOrGetter } from 'vue'
import { computed, ref, toValue } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  deleteApiActivitiesActivityId,
  getApiActivities,
  postApiActivitiesEventId,
  putApiActivitiesActivityId,
} from '@/shared/api/generated/endpoints/activities/activities'
import type {
  ActivityResponse,
  CreateActivityRequest,
  GetApiActivitiesParams,
  UpdateActivityRequest,
} from '@/shared/api/generated/models'
import { toPage } from '@/shared/api'
import { useServerTable } from '@/shared/lib'
import { activityQueryKeys, getActivityByIdRequest } from '@/entities/activity'

export function useActivities(eventId: MaybeRefOrGetter<string>) {
  const queryClient = useQueryClient()
  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['activities'] })
    void queryClient.invalidateQueries({
      queryKey: ['reports', 'event-summary', toValue(eventId)],
    })
    void queryClient.invalidateQueries({ queryKey: ['reports', 'event-attendees'] })
    void queryClient.invalidateQueries({
      queryKey: activityQueryKeys.publicByEvent(toValue(eventId)),
    })
  }

  const modalityTypeId = ref<string | null>(null)

  const table = useServerTable<ActivityResponse, GetApiActivitiesParams>({
    queryKey: ['activities', 'admin-table'],
    fetchPage: (params) => getApiActivities(params).then(toPage),
    defaultSort: { field: 'activityStartsAt', order: 1 },
    columns: {
      title: { type: 'text' },
    },
    extraParams: () => ({
      eventId: toValue(eventId),
      modalityTypeId: modalityTypeId.value ?? undefined,
    }),
  })

  const options = useQuery({
    queryKey: computed(() => ['activities', 'event-options', toValue(eventId)] as const),
    queryFn: () =>
      getApiActivities({ eventId: toValue(eventId), pageSize: 100, sort: 'activityStartsAt' }).then(
        (response) => response.data.items ?? [],
      ),
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

  function fetchOne(activityId: string) {
    return getActivityByIdRequest(activityId)
  }

  return { table, modalityTypeId, options, create, update, remove, fetchOne }
}
