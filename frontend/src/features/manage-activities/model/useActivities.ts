import type { MaybeRefOrGetter } from 'vue'
import { computed, ref, toValue } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import type {
  ActivityResponse,
  CreateActivityRequest,
  GetApiActivitiesParams,
  UpdateActivityRequest,
} from '@/shared/api/generated/models'
import { useServerTable } from '@/shared/lib'
import {
  activityQueryKeys,
  createActivityRequest,
  deleteActivityRequest,
  getActivitiesAdminPageRequest,
  getActivityByIdRequest,
  getEventActivityOptionsRequest,
  updateActivityRequest,
} from '@/entities/activity'
import { eventReportQueryKeys } from '@/entities/event'

export function useActivities(eventId: MaybeRefOrGetter<string>) {
  const queryClient = useQueryClient()
  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: activityQueryKeys.all })
    void queryClient.invalidateQueries({
      queryKey: eventReportQueryKeys.summary(toValue(eventId)),
    })
    void queryClient.invalidateQueries({ queryKey: eventReportQueryKeys.attendees() })
  }

  const modalityTypeId = ref<string | null>(null)

  const table = useServerTable<ActivityResponse, GetApiActivitiesParams>({
    queryKey: activityQueryKeys.adminTable(),
    fetchPage: (params) => getActivitiesAdminPageRequest(params),
    defaultSort: { field: 'activityStartsAt', order: 1 },
    columns: {
      title: { type: 'text' },
      activityDate: { type: 'dateRange', fromParam: 'activityDateFrom', toParam: 'activityDateTo' },
    },
    extraParams: () => ({
      eventId: toValue(eventId),
      modalityTypeId: modalityTypeId.value ?? undefined,
    }),
  })

  const options = useQuery({
    queryKey: computed(() => activityQueryKeys.eventOptions(toValue(eventId))),
    queryFn: () => getEventActivityOptionsRequest(toValue(eventId)),
  })

  const create = useMutation({
    mutationFn: (body: CreateActivityRequest) => createActivityRequest(toValue(eventId), body),
    onSuccess: invalidate,
  })

  const update = useMutation({
    mutationFn: (vars: { id: string; body: UpdateActivityRequest }) =>
      updateActivityRequest(vars.id, vars.body),
    onSuccess: invalidate,
  })

  const remove = useMutation({
    mutationFn: (id: string) => deleteActivityRequest(id),
    onSuccess: invalidate,
  })

  function fetchOne(activityId: string) {
    return getActivityByIdRequest(activityId)
  }

  return { table, modalityTypeId, options, create, update, remove, fetchOne }
}
