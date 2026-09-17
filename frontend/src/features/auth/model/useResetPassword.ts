import { ref, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMutation } from '@tanstack/vue-query'

import { getErrorMessage } from '@/shared/lib'

import { resetPasswordRequest } from '../api/requests'

/** Reset page step: the password form, or the confirmation shown after a successful reset. */
export type ResetPasswordState = 'form' | 'success'

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/**
 * Reset-password form for the emailed link. Validates length (12+) and confirmation locally before
 * calling the API; an API failure shows its message and offers requesting a new link.
 *
 * @param userId - User id from the link; `hasValidLink` requires it to be a GUID.
 * @param code - One-time reset code from the link.
 */
export function useResetPassword(userId: string | null, code: string | null) {
  const { t } = useI18n()
  const form = reactive({ password: '', confirmPassword: '' })
  const state = ref<ResetPasswordState>('form')
  const errorMessage = ref<string | null>(null)
  const canRequestNewLink = ref(false)

  const mutation = useMutation({
    mutationFn: (payload: { userId: string; code: string; password: string }) =>
      resetPasswordRequest(payload.userId, payload.code, payload.password),
    onSuccess: () => {
      state.value = 'success'
    },
    onError: (error) => {
      errorMessage.value = getErrorMessage(error)
      canRequestNewLink.value = true
    },
  })

  function submit(): void {
    errorMessage.value = null
    canRequestNewLink.value = false
    if (form.password.length < 12) {
      errorMessage.value = t('validation.newPasswordMin')
      return
    }
    if (form.password !== form.confirmPassword) {
      errorMessage.value = t('validation.passwordsMismatch')
      return
    }
    if (!userId || !code) return
    mutation.mutate({ userId, code, password: form.password })
  }

  return {
    form,
    state,
    errorMessage,
    canRequestNewLink,
    submit,
    hasValidLink: userId !== null && GUID_PATTERN.test(userId) && code !== null,
    isSubmitting: mutation.isPending,
  }
}
