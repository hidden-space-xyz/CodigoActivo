import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  accountQueryKeys,
  addAccountChildRequest,
  changeAccountPasswordRequest,
  deleteAccountChildRequest,
  deleteAccountRequest,
  getAccountChildrenRequest,
  getAccountProfileRequest,
  getRegistrationTypesRequest,
  updateAccountChildRequest,
  updateAccountProfileRequest,
} from '@/entities/account'
import { activityQueryKeys } from '@/entities/activity'
import type {
  AccountProfile,
  AddMinorInput,
  ChangePasswordInput,
  UpdateMinorInput,
  UpdateProfileInput,
} from '@/entities/account'
import { getCurrentUserRequest, logoutRequest, useSession } from '@/entities/session'

export function useAccount() {
  const session = useSession()
  const queryClient = useQueryClient()
  const router = useRouter()

  const userId = computed(() => session.user?.id ?? null)
  const profileKey = accountQueryKeys.me()
  const childrenKey = accountQueryKeys.children()

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

  const minorRoles = useQuery({
    queryKey: accountQueryKeys.registrationTypes(true),
    queryFn: () => getRegistrationTypesRequest(true),
  })

  function invalidateChildren(): void {
    void queryClient.invalidateQueries({ queryKey: childrenKey })
    // The event-signup household dialog caches the same members under the activity entity's key;
    // keep it in sync so a newly added/removed minor shows up there without waiting for staleTime.
    void queryClient.invalidateQueries({ queryKey: activityQueryKeys.householdMembers() })
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

  const deleteChild = useMutation({
    mutationFn: (childId: string) => deleteAccountChildRequest(childId),
    onSuccess: invalidateChildren,
  })

  const deleteOwnAccount = useMutation({
    mutationFn: () => {
      if (!userId.value) return Promise.reject(new Error('No autenticado'))
      return deleteAccountRequest(userId.value)
    },
    onSuccess: async () => {
      try {
        await logoutRequest()
      } finally {
        session.clear()
        queryClient.clear()
        await router.push({ name: 'home' })
      }
    },
  })

  return {
    profile,
    children,
    minorRoles,
    updateProfile,
    changePassword,
    addChild,
    updateChild,
    deleteChild,
    deleteOwnAccount,
  }
}
