import { computed, reactive, ref } from 'vue'

import { getCurrentUserRequest } from '../api/requests'
import type { AuthUser } from './types'

const user = ref<AuthUser | null>(null)

let inflight: Promise<AuthUser | null> | null = null

const isAuthenticated = computed(() => user.value !== null)
const displayName = computed(() => user.value?.firstName ?? '')
const isAdmin = computed(() => user.value?.isAdmin ?? false)

function setUser(value: AuthUser | null): void {
  user.value = value
}

function clear(): void {
  user.value = null
}

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
