import { computed, reactive } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { sessionQueries } from '../api/queries'
import { hasStaffAccess } from './access'

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
    isEditor: computed(() => hasStaffAccess(user.value, 'editor')),
    isAdmin: computed(() => hasStaffAccess(user.value, 'admin')),
    isSuperAdmin: computed(() => user.value?.isSuperAdmin ?? false),
    isPrivacyOfficer: computed(() => user.value?.isPrivacyOfficer ?? false),
    displayName: computed(() => user.value?.firstName ?? ''),
  })
}
