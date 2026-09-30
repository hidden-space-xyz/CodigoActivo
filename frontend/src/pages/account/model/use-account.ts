import { computed } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  accountKeys,
  accountMutations,
  accountQueries,
  type AccountProfile,
} from '@/entities/account'
import { activityKeys } from '@/entities/activity'
import { refreshSession, useSession } from '@/entities/session'
import { alsoInvalidates } from '@/shared/api'

/**
 * Signed-in user's profile, minors and account mutations. A saved profile replaces the cached one
 * and refreshes the session user; minor changes also refresh the household members offered for
 * activity signup. Deleting the own account lives in `useDeleteAccount`, because it needs the
 * password and the second factor.
 */
export function useAccount() {
  const session = useSession()
  const queryClient = useQueryClient()
  const userId = computed(() => session.user?.id ?? '')

  const profile = useQuery(accountQueries.profile())
  const children = useQuery(() => ({
    ...accountQueries.children(userId.value),
    enabled: userId.value !== '',
  }))

  function syncProfile(updated: AccountProfile): void {
    queryClient.setQueryData(accountKeys.profile(), updated)
    void refreshSession(queryClient)
  }

  const updateProfile = useMutation(() => ({
    ...accountMutations.updateProfile(userId.value),
    onSuccess: syncProfile,
  }))
  const changePassword = useMutation(() => accountMutations.changePassword(userId.value))
  const addChild = useMutation(() =>
    alsoInvalidates(accountMutations.addChild(userId.value), activityKeys.householdMembers()),
  )
  const updateChild = useMutation(() =>
    alsoInvalidates(accountMutations.updateChild(userId.value), activityKeys.householdMembers()),
  )
  const deleteChild = useMutation(
    alsoInvalidates(accountMutations.removeChild(), activityKeys.householdMembers()),
  )

  return { profile, children, updateProfile, changePassword, addChild, updateChild, deleteChild }
}
