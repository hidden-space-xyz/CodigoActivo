import { computed } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import { getApiAuthMe } from '@/shared/api/generated/endpoints/auth/auth'
import {
  deleteApiUsersUserId,
  getApiUsersRegistrationTypes,
  getApiUsersUserIdChildren,
  patchApiUsersUserIdPassword,
  patchApiUsersUserIdRole,
  postApiUsersUserIdChildren,
  putApiUsersUserId,
} from '@/shared/api/generated/endpoints/users/users'
import type {
  ChangePasswordRequest,
  RegisterMinorRequest,
  UpdateUserRequest,
  UserResponse,
} from '@/shared/api/generated/models'
import { useSessionStore } from '@/modules/auth/presentation/stores/session.store'

/**
 * Self-service account area: the signed-in adult's own profile plus the minors
 * in their care, with the actions to edit data, change roles and password, and
 * add or remove minors.
 */
export function useAccount() {
  const session = useSessionStore()
  const queryClient = useQueryClient()

  const userId = computed(() => session.user?.id ?? null)

  const childrenKey = ['account', 'children'] as const

  const profile = useQuery({
    queryKey: ['account', 'me'],
    queryFn: ({ signal }) => getApiAuthMe({ signal }).then((r) => r.data),
    initialData: () => session.user ?? undefined,
  })

  const children = useQuery({
    queryKey: childrenKey,
    queryFn: ({ signal }) => {
      if (!userId.value) return Promise.resolve([] as UserResponse[])
      return getApiUsersUserIdChildren(userId.value, { signal }).then((r) => r.data ?? [])
    },
    enabled: computed(() => userId.value !== null),
  })

  const adultRoles = useQuery({
    queryKey: ['registration-types', 'adult'],
    queryFn: ({ signal }) =>
      getApiUsersRegistrationTypes({ minor: false }, { signal }).then((r) => r.data ?? []),
  })
  const minorRoles = useQuery({
    queryKey: ['registration-types', 'minor'],
    queryFn: ({ signal }) =>
      getApiUsersRegistrationTypes({ minor: true }, { signal }).then((r) => r.data ?? []),
  })

  function invalidateChildren(): void {
    void queryClient.invalidateQueries({ queryKey: childrenKey })
  }

  const updateProfile = useMutation({
    mutationFn: (request: UpdateUserRequest) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return putApiUsersUserId(userId.value, request).then((r) => r.data)
    },
    onSuccess: (updated) => {
      session.setUser(updated)
      void queryClient.invalidateQueries({ queryKey: ['account', 'me'] })
    },
  })

  const changeOwnRole = useMutation({
    mutationFn: (roleId: string) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return patchApiUsersUserIdRole(userId.value, { roleId }).then((r) => r.data)
    },
    onSuccess: (updated) => {
      session.setUser(updated)
      void queryClient.invalidateQueries({ queryKey: ['account', 'me'] })
    },
  })

  const changePassword = useMutation({
    mutationFn: (request: ChangePasswordRequest) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return patchApiUsersUserIdPassword(userId.value, request)
    },
  })

  const addChild = useMutation({
    mutationFn: (request: RegisterMinorRequest) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return postApiUsersUserIdChildren(userId.value, request).then((r) => r.data)
    },
    onSuccess: invalidateChildren,
  })

  const updateChild = useMutation({
    mutationFn: (vars: { childId: string; request: UpdateUserRequest }) =>
      putApiUsersUserId(vars.childId, vars.request).then((r) => r.data),
    onSuccess: invalidateChildren,
  })

  const changeChildRole = useMutation({
    mutationFn: (vars: { childId: string; roleId: string }) =>
      patchApiUsersUserIdRole(vars.childId, { roleId: vars.roleId }).then((r) => r.data),
    onSuccess: invalidateChildren,
  })

  const deleteChild = useMutation({
    mutationFn: (childId: string) => deleteApiUsersUserId(childId),
    onSuccess: invalidateChildren,
  })

  return {
    userId,
    profile,
    children,
    adultRoles,
    minorRoles,
    updateProfile,
    changeOwnRole,
    changePassword,
    addChild,
    updateChild,
    changeChildRole,
    deleteChild,
  }
}
