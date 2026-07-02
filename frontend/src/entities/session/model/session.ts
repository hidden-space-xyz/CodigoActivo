import { computed, reactive, ref } from 'vue'

import type { AuthUser } from './types'

const ADMIN_ROLE_ID = '6a8fbafe-22da-4dcc-8b4f-e8b5f43528b2'

const user = ref<AuthUser | null>(null)

const isAuthenticated = computed(() => user.value !== null)
const displayName = computed(() => user.value?.firstName ?? '')
const roleNames = computed(() =>
  (user.value?.roles ?? []).map((role) => role.name).filter((name) => name.length > 0),
)
const isAdmin = computed(() => (user.value?.roles ?? []).some((role) => role.id === ADMIN_ROLE_ID))

function setUser(value: AuthUser | null): void {
  user.value = value
}

function clear(): void {
  user.value = null
}

const session = reactive({
  user,
  isAuthenticated,
  displayName,
  roleNames,
  isAdmin,
  setUser,
  clear,
})

export function useSession() {
  return session
}
