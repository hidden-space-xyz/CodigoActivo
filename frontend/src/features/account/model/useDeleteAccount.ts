import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  accountQueryKeys,
  deleteAccountRequest,
  getAccountDeletionAllowedRequest,
  requestAccountDeletionCodeRequest,
} from '@/entities/account'
import type { DeleteAccountInput } from '@/entities/account'
import { logoutRequest, useSession } from '@/entities/session'
import { TwoFactorMethod } from '@/shared/api/generated/models'
import { getErrorMessage, useCrudFeedback } from '@/shared/lib'

/**
 * Self-service deletion of the signed-in account. It is a two-step confirmation: the password
 * unlocks the operation and a second-factor code authorizes it, either emailed on demand with
 * `requestCode` or read from the user's authenticator application. Once the API confirms, the
 * session and every cached query are dropped and the browser lands on the home page, because the
 * account behind them no longer exists. Only administrators ask the API whether they may delete
 * their account, since the last administrator may not; until it answers, `canDelete` stays false.
 */
export function useDeleteAccount() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const session = useSession()
  const queryClient = useQueryClient()
  const router = useRouter()

  const method = computed(() => session.user?.twoFactorMethod ?? TwoFactorMethod.Email)
  const isAuthenticator = computed(() => method.value === TwoFactorMethod.Authenticator)
  const email = computed(() => session.user?.email ?? '')
  const errorMessage = ref('')

  const isAdmin = computed(() => session.isAdmin)
  const deletionAllowed = useQuery({
    queryKey: accountQueryKeys.deletionAllowed(),
    queryFn: getAccountDeletionAllowedRequest,
    enabled: isAdmin,
  })
  const canDelete = computed(() => !isAdmin.value || deletionAllowed.data.value === true)
  const isLastAdmin = computed(() => isAdmin.value && deletionAllowed.data.value === false)

  function reset(): void {
    errorMessage.value = ''
  }

  const requestCode = useMutation({
    mutationFn: (currentPassword: string) => requestAccountDeletionCodeRequest(currentPassword),
    onSuccess: reset,
    onError: (error) => {
      errorMessage.value = getErrorMessage(error)
    },
  })

  const confirm = useMutation({
    mutationFn: (input: DeleteAccountInput) => deleteAccountRequest(input),
    onSuccess: async () => {
      reset()
      try {
        await logoutRequest()
      } catch {
        // The API already ended the session; a failed sign-out must not strand the user.
      }
      session.clear()
      queryClient.clear()
      await router.push({ name: 'home' })
      feedback.success(
        t('features.account.deleteAccount.deletedDetail'),
        t('features.account.deleteAccount.deletedSummary'),
      )
    },
    onError: (error) => {
      errorMessage.value = getErrorMessage(error)
    },
  })

  return {
    method,
    isAuthenticator,
    email,
    canDelete,
    isLastAdmin,
    errorMessage,
    reset,
    requestCode,
    confirm,
  }
}
