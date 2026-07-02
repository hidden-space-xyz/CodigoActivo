import type { MaybeRefOrGetter } from 'vue'
import { computed, toValue } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { fetchODataFunction, odataGuid } from '@/shared/api'
import type { ActivityAssignmentsReportResponse } from '@/shared/api'

export function useActivityAssignments(activityId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => ['reports', 'activity-assignments', toValue(activityId)] as const),
    queryFn: () =>
      fetchODataFunction<ActivityAssignmentsReportResponse>('ActivityAssignments', {
        activityId: odataGuid(toValue(activityId)),
      }),
  })
}
