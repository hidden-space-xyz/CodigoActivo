import type { MaybeRefOrGetter } from 'vue'
import { computed, toValue } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { getApiReportsActivityActivityIdAssignments } from '@/shared/api/generated/endpoints/reports/reports'

export function useActivityAssignments(activityId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => ['reports', 'activity-assignments', toValue(activityId)] as const),
    queryFn: ({ signal }) =>
      getApiReportsActivityActivityIdAssignments(toValue(activityId), { signal }).then(
        (r) => r.data,
      ),
  })
}
