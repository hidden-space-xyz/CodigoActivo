import { computed, ref } from 'vue'
import { useMutation } from '@tanstack/vue-query'

import { getErrorMessage, useCrudFeedback } from '@/shared/lib'

import { resendVerificationRequest, verifyRegistrationRequest } from '../api/requests'

export type LinkVerificationState = 'verifying' | 'success' | 'error'

export function useAccountVerification() {
  const feedback = useCrudFeedback()

  const state = ref<LinkVerificationState>('verifying')
  const errorMessage = ref<string | null>(null)
  const userId = ref<string | null>(null)

  const mutation = useMutation({
    mutationFn: (payload: { userId: string; code: string }) =>
      verifyRegistrationRequest(payload.userId, payload.code),
    onSuccess: () => {
      state.value = 'success'
    },
    onError: (error) => {
      state.value = 'error'
      errorMessage.value = getErrorMessage(error)
    },
  })

  const resendMutation = useMutation({
    mutationFn: () => {
      if (!userId.value) throw new Error('missing user id')
      return resendVerificationRequest(userId.value)
    },
    onSuccess: () => {
      feedback.success(
        'Te hemos enviado un nuevo enlace de verificación. Revisa tu correo.',
        'Código reenviado',
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
      errorMessage.value = 'El enlace de verificación no es válido o está incompleto.'
      return
    }
    state.value = 'verifying'
    errorMessage.value = null
    mutation.mutate({ userId: id, code })
  }

  function resend(): void {
    if (userId.value && !resendMutation.isPending.value) resendMutation.mutate()
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
