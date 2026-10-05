import { computed, ref } from 'vue'
import { useMutation, useQuery } from '@tanstack/vue-query'

import { accountMutations, accountQueries } from '@/entities/account'
import { useSession } from '@/entities/session'

/**
 * User's event participation history split into upcoming and past entries, plus the mutation that
 * saves an event rating and refetches the history. Ratings are anonymous, so the API cannot tell
 * which events the user already rated; the events rated while this view is open are remembered to
 * avoid sending the same opinion twice by mistake.
 */
export function useAccountHistory() {
  const session = useSession()

  const history = useQuery(() => ({
    ...accountQueries.history(),
    enabled: session.user !== null,
  }))
  const entries = computed(() => history.data.value ?? [])
  const upcoming = computed(() => entries.value.filter((entry) => !entry.isPast))
  const past = computed(() => entries.value.filter((entry) => entry.isPast))

  const ratedEventIds = ref<ReadonlySet<string>>(new Set())
  const saveRating = useMutation({
    ...accountMutations.rateEvent(),
    onSuccess: (_result, { eventId }) => {
      ratedEventIds.value = new Set([...ratedEventIds.value, eventId])
    },
  })

  function isRated(eventId: string): boolean {
    return ratedEventIds.value.has(eventId)
  }

  return { history, entries, upcoming, past, saveRating, isRated }
}
