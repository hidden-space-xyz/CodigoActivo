import { computed, ref } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'

import { accountMutations, accountQueries } from '@/entities/account'
import { endSession, logoutRequest, useSession } from '@/entities/session'
import { getErrorMessage, useCrudFeedback } from '@/shared/lib/feedback'

/**
 * Self-service deletion of the signed-in account. It is a two-step confirmation: the password
 * unlocks the operation and a second-factor code authorizes it, either emailed on demand with
 * `requestCode` or read from the user's authenticator application. Once the API confirms, the
 * session and every cached query are dropped and the browser lands on the home page, because the
 * account behind them no longer exists; a failed sign-out is ignored, since the API already ended
 * the session. Only administrators ask the API whether they may delete their account, since the
 * initial administrator may not; until it answers, `canDelete` stays false.
 */
export function useDeleteAccount() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const session = useSession()
  const queryClient = useQueryClient()
  const router = useRouter()

  const isAuthenticator = computed(() => session.user?.twoFactorMethod === 'Authenticator')
  const email = computed(() => session.user?.email ?? '')
  const errorMessage = ref('')

  const deletionAllowed = useQuery(() => ({
    ...accountQueries.deletionAllowed(),
    enabled: session.isAdmin,
  }))
  const canDelete = computed(() => !session.isAdmin || deletionAllowed.data.value === true)
  const isInitialAdmin = computed(() => session.isAdmin && deletionAllowed.data.value === false)

  function reset(): void {
    errorMessage.value = ''
  }

  function showError(error: unknown): void {
    errorMessage.value = getErrorMessage(error)
  }

  const requestCode = useMutation({
    ...accountMutations.requestDeletionCode(),
    onSuccess: reset,
    onError: showError,
  })

  const confirm = useMutation({
    ...accountMutations.deleteAccount(),
    onSuccess: async () => {
      reset()
      await logoutRequest().catch(() => undefined)
      endSession(queryClient)
      await router.push({ name: 'home' })
      feedback.success(
        t('pages.account.deleteAccount.deletedDetail'),
        t('pages.account.deleteAccount.deletedSummary'),
      )
    },
    onError: showError,
  })

  return {
    isAuthenticator,
    email,
    canDelete,
    isInitialAdmin,
    errorMessage,
    reset,
    requestCode,
    confirm,
  }
}
