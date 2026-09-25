import { computed, type MaybeRefOrGetter } from 'vue'

import { useEventLeaderRoster } from '@/entities/event'
import { useSession } from '@/entities/session'

/**
 * Attendee lists of the event's activities that the signed-in user leads with a confirmed
 * assignment and that have not ended. Guests never request them, and `hasActivities` stays
 * `false` until the API returns at least one led activity, so the page only offers the tab to
 * confirmed leaders. The API is the only authority on what the user may see.
 *
 * @param eventId - Getter or ref so the list follows route changes.
 */
export function useLeaderRoster(eventId: MaybeRefOrGetter<string>) {
  const session = useSession()
  const query = useEventLeaderRoster(eventId, () => session.user?.id ?? null)

  const activities = computed(() => query.data.value ?? [])

  return {
    activities,
    hasActivities: computed(() => activities.value.length > 0),
    isLoading: query.isLoading,
    isError: query.isError,
  }
}
