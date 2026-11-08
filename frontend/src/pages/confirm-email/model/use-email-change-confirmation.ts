import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMutation, useQueryClient } from '@tanstack/vue-query'

import { accountMutations } from '@/entities/account'
import { refreshSession } from '@/entities/session'
import { hasErrorCode } from '@/shared/api'
import { getErrorMessage } from '@/shared/lib/feedback'
import { isGuid } from '@/shared/lib/navigation'

/** Progress of confirming the emailed link: in flight, confirmed, or failed/incomplete. */
export type EmailChangeConfirmationState = 'confirming' | 'success' | 'error'

/**
 * Confirmation page flow: `confirm` sends the link's user id and code (an incomplete link, or one
 * whose user id is not a GUID, fails without calling the API). Once the account uses its new email
 * the session is refreshed and the cached profile invalidated, so a holder signed in on this
 * browser sees the new address. A link the API refuses as spent or expired sets `staleLink`: the
 * change may be confirmed already.
 */
export function useEmailChangeConfirmation() {
  const { t } = useI18n()
  const queryClient = useQueryClient()

  const state = ref<EmailChangeConfirmationState>('confirming')
  const errorMessage = ref('')
  const staleLink = ref(false)

  const mutation = useMutation({
    ...accountMutations.confirmEmailChange(),
    onSuccess: () => {
      state.value = 'success'
      void refreshSession(queryClient)
    },
    onError: (error) => {
      state.value = 'error'
      staleLink.value = hasErrorCode(error, 'OtpInvalidOrExpired')
      errorMessage.value = staleLink.value
        ? t('pages.confirmEmail.staleLink')
        : getErrorMessage(error)
    },
  })

  function confirm(userId: string | null, code: string | null): void {
    staleLink.value = false
    if (!isGuid(userId) || !code) {
      state.value = 'error'
      errorMessage.value = t('pages.confirmEmail.incompleteLink')
      return
    }
    mutation.mutate({ userId, code })
  }

  return { state, errorMessage, staleLink, confirm }
}
