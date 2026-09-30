import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMutation } from '@tanstack/vue-query'

import { accountMutations } from '@/entities/account'
import { getErrorMessage, useCrudFeedback } from '@/shared/lib/feedback'

/** Progress of verifying the emailed link: in flight, activated, or failed/incomplete. */
export type LinkVerificationState = 'verifying' | 'success' | 'error'

/**
 * Verification page flow: `verify` checks the link's user id and code (an incomplete link fails
 * without calling the API), and `resend` requests a new link, reporting the result as a toast.
 */
export function useAccountVerification() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()

  const state = ref<LinkVerificationState>('verifying')
  const errorMessage = ref<string | null>(null)
  const userId = ref<string | null>(null)

  const mutation = useMutation({
    ...accountMutations.verify(),
    onSuccess: () => {
      state.value = 'success'
    },
    onError: (error) => {
      state.value = 'error'
      errorMessage.value = getErrorMessage(error)
    },
  })

  const resendMutation = useMutation({
    ...accountMutations.resendVerification(),
    onSuccess: () => {
      feedback.success(
        t('entities.account.verification.linkResentDetail'),
        t('entities.account.verification.linkResentSummary'),
      )
    },
    onError: (error) => {
      feedback.error(error)
    },
  })

  function verify(id: string | null, code: string | null): void {
    userId.value = id
    if (!id || !code) {
      state.value = 'error'
      errorMessage.value = t('pages.verifyAccount.incompleteLink')
      return
    }
    state.value = 'verifying'
    errorMessage.value = null
    mutation.mutate({ userId: id, otp: code })
  }

  function resend(): void {
    if (userId.value && !resendMutation.isPending.value) resendMutation.mutate(userId.value)
  }

  return {
    state,
    errorMessage,
    verify,
    resend,
    canResend: computed(() => userId.value !== null),
    isResending: resendMutation.isPending,
  }
}
