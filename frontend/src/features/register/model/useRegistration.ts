import { onScopeDispose, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMutation } from '@tanstack/vue-query'

import { scrollToTop, useCrudFeedback } from '@/shared/lib'

import { registerRequest, resendVerificationRequest } from '../api/requests'
import { createEmptyRegistrationForm, type RegistrationForm } from './registration-form'

export type RegistrationStep = 'age-gate' | 'form' | 'success'

const RESEND_COOLDOWN_SECONDS = 60

export function useRegistration() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()

  const step = ref<RegistrationStep>('age-gate')
  const form = reactive<RegistrationForm>(createEmptyRegistrationForm())
  const createdUserId = ref<string | null>(null)
  const requiresVerification = ref(false)
  const submittedEmail = ref('')
  const submittedMinorCount = ref(0)
  const resendCooldown = ref(0)

  let cooldownTimer: ReturnType<typeof setInterval> | null = null

  function stopCooldown(): void {
    if (cooldownTimer !== null) {
      clearInterval(cooldownTimer)
      cooldownTimer = null
    }
  }

  function startCooldown(): void {
    stopCooldown()
    const deadline = Date.now() + RESEND_COOLDOWN_SECONDS * 1000
    const tick = (): void => {
      resendCooldown.value = Math.max(0, Math.ceil((deadline - Date.now()) / 1000))
      if (resendCooldown.value <= 0) stopCooldown()
    }
    tick()
    cooldownTimer = setInterval(tick, 1000)
  }

  onScopeDispose(stopCooldown)

  const mutation = useMutation({
    mutationFn: (payload: RegistrationForm) => registerRequest(payload),
    onSuccess: (result) => {
      createdUserId.value = result.adultId
      requiresVerification.value = result.requiresVerification
      submittedEmail.value = form.email.trim()
      submittedMinorCount.value = result.minorCount || form.minors.length
      step.value = 'success'
      if (result.requiresVerification) startCooldown()
      scrollToTop()
    },
    onError: (error) => {
      feedback.error(error)
    },
  })

  const resendMutation = useMutation({
    mutationFn: () => {
      if (!createdUserId.value) throw new Error('missing user id')
      return resendVerificationRequest(createdUserId.value)
    },
    onSuccess: () => {
      startCooldown()
      feedback.success(
        t('features.register.toast.linkResentDetail'),
        t('features.register.toast.linkResentSummary'),
      )
    },
    onError: (error) => {
      feedback.error(error)
    },
  })

  function confirmAdult(): void {
    step.value = 'form'
    scrollToTop()
  }

  function backToGate(): void {
    step.value = 'age-gate'
    scrollToTop()
  }

  function submit(): void {
    mutation.mutate({ ...form })
  }

  function resend(): void {
    if (resendCooldown.value > 0 || resendMutation.isPending.value) return
    resendMutation.mutate()
  }

  function reset(): void {
    mutation.reset()
    resendMutation.reset()
    stopCooldown()
    Object.assign(form, createEmptyRegistrationForm())
    createdUserId.value = null
    requiresVerification.value = false
    submittedEmail.value = ''
    submittedMinorCount.value = 0
    resendCooldown.value = 0
    step.value = 'age-gate'
    scrollToTop()
  }

  return {
    step,
    form,
    requiresVerification,
    submittedEmail,
    submittedMinorCount,
    resendCooldown,
    confirmAdult,
    backToGate,
    submit,
    resend,
    reset,
    isSubmitting: mutation.isPending,
    isResending: resendMutation.isPending,
  }
}
