import type { MaybeRefOrGetter } from 'vue'
import { computed, toValue } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { getApiReportsActivitiesActivityIdAssignments } from '@/shared/api/generated/endpoints/reports/reports'

export function useActivityAssignments(activityId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => ['reports', 'activity-assignments', toValue(activityId)] as const),
    queryFn: () =>
      getApiReportsActivitiesActivityIdAssignments(toValue(activityId)).then((r) => r.data),
  })
}
