import { computed } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  accountQueryKeys,
  getAccountHistoryRequest,
  saveAccountEventRatingRequest,
} from '@/entities/account'
import type { AccountHistoryEntry, EventRatingInput } from '@/entities/account'
import { ApiError } from '@/shared/api'
import { ErrorCode } from '@/shared/api/generated/models'
import { useSession } from '@/entities/session'

/**
 * User's event participation history split into upcoming and past entries, plus the mutation that
 * saves an event rating and refetches the history.
 */
export function useAccountHistory() {
  const session = useSession()
  const queryClient = useQueryClient()

  const userId = computed(() => session.user?.id ?? null)
  const historyKey = accountQueryKeys.history()

  const history = useQuery({
    queryKey: historyKey,
    queryFn: () => getAccountHistoryRequest(),
    enabled: computed(() => userId.value !== null),
  })

  const entries = computed<readonly AccountHistoryEntry[]>(() => history.data.value ?? [])
  const upcoming = computed(() => entries.value.filter((entry) => !entry.isPast))
  const past = computed(() => entries.value.filter((entry) => entry.isPast))

  const saveRating = useMutation({
    mutationFn: (vars: { eventId: string; input: EventRatingInput }) =>
      saveAccountEventRatingRequest(vars.eventId, vars.input),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: historyKey })
    },
    onError: (error: unknown) => {
      // A stale "already submitted" conflict means the history shown no longer matches the server.
      if (error instanceof ApiError && error.code === ErrorCode.EventRatingAlreadySubmitted) {
        void queryClient.invalidateQueries({ queryKey: historyKey })
      }
    },
  })

  return { history, entries, upcoming, past, saveRating }
}
