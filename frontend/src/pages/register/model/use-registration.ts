import { reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMutation } from '@tanstack/vue-query'

import { accountMutations } from '@/entities/account'
import { useCountdown } from '@/shared/lib/countdown'
import { useCrudFeedback } from '@/shared/lib/feedback'
import { scrollToTop } from '@/shared/lib/navigation'

import {
  createEmptyRegistrationForm,
  toRegistrationInput,
  type RegistrationForm,
} from './registration-form'

/** Screen of the registration flow: adult confirmation, data entry, or the result. */
export type RegistrationStep = 'age-gate' | 'form' | 'success'

const RESEND_COOLDOWN_SECONDS = 60

/**
 * Drives the registration flow through its steps, scrolling to the top on each change. Every new
 * account must verify its email, so after registering, resending the email is locked by a
 * 60-second cooldown (restarted on every resend). `reset` clears the form and returns to the age gate.
 */
export function useRegistration() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()

  const step = ref<RegistrationStep>('age-gate')
  const form = reactive<RegistrationForm>(createEmptyRegistrationForm())
  const createdUserId = ref<string | null>(null)
  const submittedEmail = ref('')
  const submittedMinorCount = ref(0)
  const cooldown = useCountdown(RESEND_COOLDOWN_SECONDS)

  const mutation = useMutation({
    ...accountMutations.register(),
    onSuccess: (result) => {
      createdUserId.value = result.adultId
      submittedEmail.value = form.email.trim()
      submittedMinorCount.value = result.minorCount || form.minors.length
      step.value = 'success'
      cooldown.start()
      scrollToTop()
    },
    onError: (error) => {
      feedback.error(error)
    },
  })

  const resendMutation = useMutation({
    ...accountMutations.resendVerification(),
    onSuccess: () => {
      cooldown.start()
      feedback.success(
        t('entities.account.verification.linkResentDetail'),
        t('entities.account.verification.linkResentSummary'),
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
    const input = toRegistrationInput(form)
    if (input) mutation.mutate(input)
  }

  function resend(): void {
    const userId = createdUserId.value
    if (!userId || cooldown.remaining.value > 0 || resendMutation.isPending.value) return
    resendMutation.mutate(userId)
  }

  function reset(): void {
    mutation.reset()
    resendMutation.reset()
    cooldown.stop()
    Object.assign(form, createEmptyRegistrationForm())
    createdUserId.value = null
    submittedEmail.value = ''
    submittedMinorCount.value = 0
    step.value = 'age-gate'
    scrollToTop()
  }

  return {
    step,
    form,
    submittedEmail,
    submittedMinorCount,
    resendCooldown: cooldown.remaining,
    confirmAdult,
    backToGate,
    submit,
    resend,
    reset,
    isSubmitting: mutation.isPending,
    isResending: resendMutation.isPending,
  }
}
