import { computed } from 'vue'
import { useMutation, useQuery } from '@tanstack/vue-query'

import { accountMutations, accountQueries } from '@/entities/account'
import { useSession } from '@/entities/session'

/**
 * User's event participation history split into upcoming and past entries, plus the mutation that
 * saves an event rating and refetches the history.
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

  const saveRating = useMutation(accountMutations.rateEvent())

  return { history, entries, upcoming, past, saveRating }
}
