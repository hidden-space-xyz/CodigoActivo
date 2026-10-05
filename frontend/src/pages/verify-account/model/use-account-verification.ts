import { reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMutation } from '@tanstack/vue-query'

import { accountMutations } from '@/entities/account'
import { getErrorMessage, useCrudFeedback } from '@/shared/lib/feedback'

/** Progress of verifying the emailed link: in flight, activated, or failed/incomplete. */
export type LinkVerificationState = 'verifying' | 'success' | 'error'

/**
 * Verification page flow: `verify` checks the link's user id and code (an incomplete link fails
 * without calling the API), and `resend` asks for a new link for the address in `resendForm`,
 * reporting with a toast that never tells whether the address has a pending account.
 */
export function useAccountVerification() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()

  const state = ref<LinkVerificationState>('verifying')
  const errorMessage = ref<string | null>(null)
  const resendForm = reactive({ email: '' })

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
    if (!resendMutation.isPending.value) resendMutation.mutate(resendForm.email.trim())
  }

  return {
    state,
    errorMessage,
    verify,
    resendForm,
    resend,
    isResending: resendMutation.isPending,
  }
}
