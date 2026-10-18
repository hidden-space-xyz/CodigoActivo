import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMutation } from '@tanstack/vue-query'

import { unsubscribeRequest } from '../api/requests'
import { getErrorMessage } from '@/shared/lib/feedback'

/** Progress of following an unsubscribe link: in flight, done, or failed/incomplete. */
export type UnsubscribeState = 'unsubscribing' | 'success' | 'error'

/**
 * Unsubscribe page flow: `unsubscribe` sends the token of the link a news email carries,
 * which withdraws the news consent without a session. A link without token fails without
 * calling the API; following the link again succeeds again.
 */
export function useUnsubscribe() {
  const { t } = useI18n()

  const state = ref<UnsubscribeState>('unsubscribing')
  const errorMessage = ref('')

  const mutation = useMutation({
    mutationFn: unsubscribeRequest,
    onSuccess: () => {
      state.value = 'success'
    },
    onError: (error) => {
      state.value = 'error'
      errorMessage.value = getErrorMessage(error)
    },
  })

  function unsubscribe(token: string | null): void {
    if (!token) {
      state.value = 'error'
      errorMessage.value = t('pages.unsubscribe.incompleteLink')
      return
    }
    mutation.mutate(token)
  }

  return { state, errorMessage, unsubscribe }
}
