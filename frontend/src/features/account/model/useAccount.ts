import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
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
  const { t } = useI18n()
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

  function invalidateChildren(): void {
    void queryClient.invalidateQueries({ queryKey: childrenKey })
    void queryClient.invalidateQueries({ queryKey: activityQueryKeys.householdMembers() })
  }

  function syncProfile(updated: AccountProfile): void {
    queryClient.setQueryData(profileKey, updated)
    void getCurrentUserRequest().then((user) => session.setUser(user))
  }

  function withUserId<T>(run: (id: string) => Promise<T>): Promise<T> {
    const id = userId.value
    if (!id) return Promise.reject(new Error(t('features.account.notAuthenticated')))
    return run(id)
  }

  const updateProfile = useMutation({
    mutationFn: (input: UpdateProfileInput) =>
      withUserId((id) => updateAccountProfileRequest(id, input)),
    onSuccess: syncProfile,
  })

  const changePassword = useMutation({
    mutationFn: (input: ChangePasswordInput) =>
      withUserId((id) => changeAccountPasswordRequest(id, input)),
  })

  const addChild = useMutation({
    mutationFn: (input: AddMinorInput) => withUserId((id) => addAccountChildRequest(id, input)),
    onSuccess: invalidateChildren,
  })

  const updateChild = useMutation({
    mutationFn: (vars: { childId: string; input: UpdateMinorInput }) =>
      withUserId((id) => updateAccountChildRequest(vars.childId, id, vars.input)),
    onSuccess: invalidateChildren,
  })

  const deleteChild = useMutation({
    mutationFn: (childId: string) => deleteAccountChildRequest(childId),
    onSuccess: invalidateChildren,
  })

  const deleteOwnAccount = useMutation({
    mutationFn: () => withUserId((id) => deleteAccountRequest(id)),
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
    updateProfile,
    changePassword,
    addChild,
    updateChild,
    deleteChild,
    deleteOwnAccount,
  }
}
