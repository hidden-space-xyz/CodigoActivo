import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMutation, useQueryClient } from '@tanstack/vue-query'

import { accountMutations } from '@/entities/account'
import { refreshSession } from '@/entities/session'
import { getErrorMessage } from '@/shared/lib/feedback'

/** Progress of confirming the emailed link: in flight, confirmed, or failed/incomplete. */
export type EmailChangeConfirmationState = 'confirming' | 'success' | 'error'

/**
 * Confirmation page flow: `confirm` sends the link's user id and code (an incomplete link fails
 * without calling the API). Once the account uses its new email the session is refreshed, so a
 * holder signed in on this browser sees the new address.
 */
export function useEmailChangeConfirmation() {
  const { t } = useI18n()
  const queryClient = useQueryClient()

  const state = ref<EmailChangeConfirmationState>('confirming')
  const errorMessage = ref('')

  const mutation = useMutation({
    ...accountMutations.confirmEmailChange(),
    onSuccess: () => {
      state.value = 'success'
      void refreshSession(queryClient)
    },
    onError: (error) => {
      state.value = 'error'
      errorMessage.value = getErrorMessage(error)
    },
  })

  function confirm(userId: string | null, code: string | null): void {
    if (!userId || !code) {
      state.value = 'error'
      errorMessage.value = t('pages.confirmEmail.incompleteLink')
      return
    }
    mutation.mutate({ userId, code })
  }

  return { state, errorMessage, confirm }
}
