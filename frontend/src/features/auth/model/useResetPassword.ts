import { ref, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMutation } from '@tanstack/vue-query'

import { getErrorMessage } from '@/shared/lib'

import { resetPasswordRequest } from '../api/requests'

export type ResetPasswordState = 'form' | 'success'

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

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
    if (form.password.length < 8) {
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
