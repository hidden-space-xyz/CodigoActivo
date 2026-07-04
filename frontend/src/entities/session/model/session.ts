import { computed, reactive, ref } from 'vue'

import { getCurrentUserRequest } from '../api/requests'
import type { AuthUser } from './types'

const ADMIN_ROLE_ID = '6a8fbafe-22da-4dcc-8b4f-e8b5f43528b2'

const user = ref<AuthUser | null>(null)

let inflight: Promise<AuthUser | null> | null = null

const isAuthenticated = computed(() => user.value !== null)
const displayName = computed(() => user.value?.firstName ?? '')
const isAdmin = computed(() => (user.value?.roles ?? []).some((role) => role.id === ADMIN_ROLE_ID))

function setUser(value: AuthUser | null): void {
  user.value = value
}

function clear(): void {
  user.value = null
}

/**
 * Resolves the current user from the session cookie. Concurrent callers (router guards, app
 * bootstrap) share a single in-flight request instead of each hitting /api/auth/me.
 */
function resolve(): Promise<AuthUser | null> {
  if (user.value) return Promise.resolve(user.value)
  inflight ??= getCurrentUserRequest()
    .then((value) => {
      user.value = value
      return value
    })
    .finally(() => {
      inflight = null
    })
  return inflight
}

const session = reactive({
  user,
  isAuthenticated,
  displayName,
  isAdmin,
  setUser,
  clear,
  resolve,
})

export function useSession() {
  return session
}
