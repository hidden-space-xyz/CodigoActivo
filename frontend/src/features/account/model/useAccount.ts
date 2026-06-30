import { computed } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  addAccountChildRequest,
  changeAccountChildRoleRequest,
  changeAccountPasswordRequest,
  changeAccountRoleRequest,
  deleteAccountChildRequest,
  getAccountChildrenRequest,
  getAccountProfileRequest,
  getRegistrationTypesRequest,
  updateAccountChildRequest,
  updateAccountProfileRequest,
} from '@/entities/account'
import type {
  AccountProfile,
  AddMinorInput,
  ChangePasswordInput,
  UpdateMinorInput,
  UpdateProfileInput,
} from '@/entities/account'
import { getCurrentUserRequest, useSessionStore } from '@/entities/session'

export function useAccount() {
  const session = useSessionStore()
  const queryClient = useQueryClient()

  const userId = computed(() => session.user?.id ?? null)
  const profileKey = ['account', 'me'] as const
  const childrenKey = ['account', 'children'] as const

  const profile = useQuery({
    queryKey: profileKey,
    queryFn: () => getAccountProfileRequest(),
  })

  const children = useQuery({
    queryKey: childrenKey,
    queryFn: () => {
      if (!userId.value) return Promise.resolve([])
      return getAccountChildrenRequest(userId.value)
    },
    enabled: computed(() => userId.value !== null),
  })

  const adultRoles = useQuery({
    queryKey: ['registration-types', 'adult'],
    queryFn: () => getRegistrationTypesRequest(false),
  })
  const minorRoles = useQuery({
    queryKey: ['registration-types', 'minor'],
    queryFn: () => getRegistrationTypesRequest(true),
  })

  function invalidateChildren(): void {
    void queryClient.invalidateQueries({ queryKey: childrenKey })
  }

  function syncProfile(updated: AccountProfile): void {
    queryClient.setQueryData(profileKey, updated)
    void getCurrentUserRequest().then((user) => session.setUser(user))
  }

  const updateProfile = useMutation({
    mutationFn: (input: UpdateProfileInput) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return updateAccountProfileRequest(userId.value, input)
    },
    onSuccess: syncProfile,
  })

  const changeOwnRole = useMutation({
    mutationFn: (roleId: string) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return changeAccountRoleRequest(userId.value, roleId)
    },
    onSuccess: syncProfile,
  })

  const changePassword = useMutation({
    mutationFn: (input: ChangePasswordInput) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return changeAccountPasswordRequest(userId.value, input)
    },
  })

  const addChild = useMutation({
    mutationFn: (input: AddMinorInput) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return addAccountChildRequest(userId.value, input)
    },
    onSuccess: invalidateChildren,
  })

  const updateChild = useMutation({
    mutationFn: (vars: { childId: string; input: UpdateMinorInput }) => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return updateAccountChildRequest(vars.childId, userId.value, vars.input)
    },
    onSuccess: invalidateChildren,
  })

  const changeChildRole = useMutation({
    mutationFn: (vars: { childId: string; roleId: string }) =>
      changeAccountChildRoleRequest(vars.childId, vars.roleId),
    onSuccess: invalidateChildren,
  })

  const deleteChild = useMutation({
    mutationFn: (childId: string) => deleteAccountChildRequest(childId),
    onSuccess: invalidateChildren,
  })

  return {
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
