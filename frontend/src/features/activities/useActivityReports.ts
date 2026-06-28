import type { MaybeRefOrGetter } from 'vue'
import { computed, toValue } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { getApiReportsActivityActivityIdAssignments } from '@/shared/api/generated/endpoints/reports/reports'
import { queryKeys } from '@/shared/api/query-keys'

export function useActivityAssignments(activityId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => queryKeys.activityAssignments(toValue(activityId))),
    queryFn: ({ signal }) =>
      getApiReportsActivityActivityIdAssignments(toValue(activityId), { signal }).then(
        (r) => r.data,
      ),
  })
}
