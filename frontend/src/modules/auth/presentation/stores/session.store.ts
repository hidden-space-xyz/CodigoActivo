import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import type { UserResponse } from '@/shared/api/generated/models'

export const useSessionStore = defineStore('session', () => {
  const user = ref<UserResponse | null>(null)

  const isAuthenticated = computed(() => user.value !== null)
  const displayName = computed(() => user.value?.firstName ?? '')
  const roleNames = computed(() =>
    (user.value?.roles ?? []).map((role) => role.name ?? '').filter((name) => name.length > 0),
  )

  function setUser(value: UserResponse | null): void {
    user.value = value
  }

  function clear(): void {
    user.value = null
  }

  return { user, isAuthenticated, displayName, roleNames, setUser, clear }
})
