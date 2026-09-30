import { computed, reactive } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { sessionQueries } from '../api/queries'

/**
 * Reactive view of the session held in the query cache: the signed-in user (`null` for a guest)
 * and the flags derived from it. It loads the session only when nothing resolved it yet; the app
 * resolves it on startup and the route guards before protected pages. Call it in `setup`.
 */
export function useSession() {
  const query = useQuery(sessionQueries.me())
  const user = computed(() => query.data.value ?? null)

  return reactive({
    user,
    isAuthenticated: computed(() => user.value !== null),
    isAdmin: computed(() => user.value?.isAdmin ?? false),
    displayName: computed(() => user.value?.firstName ?? ''),
  })
}
