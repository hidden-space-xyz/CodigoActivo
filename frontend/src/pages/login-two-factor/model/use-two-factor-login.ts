import { computed, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import type { RouteLocationRaw } from 'vue-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'

import {
  resendTwoFactorCodeRequest,
  sessionQueries,
  startSession,
  verifyTwoFactorLoginRequest,
} from '@/entities/session'
import { hasErrorCode } from '@/shared/api'
import { useCountdown } from '@/shared/lib/countdown'
import { getErrorMessage, useCrudFeedback } from '@/shared/lib/feedback'
import { toLocalRedirect } from '@/shared/lib/navigation'

const RESEND_COOLDOWN_SECONDS = 60

function isChallengeExpired(error: unknown): boolean {
  return hasErrorCode(error, 'TwoFactorChallengeExpired')
}

/**
 * Second step of the login. Loads the pending challenge from the API (the browser holds it in a
 * cookie, so a reload survives), verifies the typed code and then stores the user in the session
 * and navigates to the `redirect` query parameter or home. `form.keepSignedIn` starts unchecked:
 * only the user's explicit choice makes the session cookie survive closing the browser. Emailed
 * codes can be resent after a 60-second cooldown, which also runs when the page opens because a
 * code was just sent. An expired challenge sends the user back to the login page.
 */
export function useTwoFactorLogin() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const queryClient = useQueryClient()
  const router = useRouter()
  const route = useRoute()

  const form = reactive({ code: '', keepSignedIn: false })
  const errorMessage = ref<string | null>(null)
  const expiredByRequest = ref(false)
  const { remaining: resendCooldown, start: startCooldown } = useCountdown(RESEND_COOLDOWN_SECONDS)

  const redirect = computed(() => toLocalRedirect(route.query.redirect))

  const loginRoute = computed<RouteLocationRaw>(() => ({
    name: 'login',
    ...(redirect.value ? { query: { redirect: redirect.value } } : {}),
  }))

  const challenge = useQuery(sessionQueries.loginChallenge())

  watch(
    () => challenge.data.value,
    (value) => {
      if (value?.method === 'Email') startCooldown()
    },
    { immediate: true },
  )

  const method = computed(() => challenge.data.value?.method ?? null)
  const maskedEmail = computed(() => challenge.data.value?.maskedEmail ?? null)
  const expired = computed(
    () =>
      expiredByRequest.value ||
      challenge.isError.value ||
      (challenge.isSuccess.value && challenge.data.value === null),
  )

  const verify = useMutation({
    mutationFn: ({ code, keepSignedIn }: { code: string; keepSignedIn: boolean }) =>
      verifyTwoFactorLoginRequest(code, keepSignedIn),
    onSuccess: (user) => {
      startSession(queryClient, user)
      void router.push(redirect.value ?? { name: 'home' })
    },
    onError: (error) => {
      if (isChallengeExpired(error)) {
        expiredByRequest.value = true
        return
      }
      errorMessage.value = getErrorMessage(error)
    },
  })

  function submit(): void {
    errorMessage.value = null
    const code = form.code.trim()
    if (!code) return
    verify.mutate({ code, keepSignedIn: form.keepSignedIn })
  }

  const resend = useMutation({
    mutationFn: () => resendTwoFactorCodeRequest(),
    onSuccess: () => {
      startCooldown()
      feedback.success(
        t('pages.loginTwoFactor.codeResentDetail'),
        t('pages.loginTwoFactor.codeResentSummary'),
      )
    },
    onError: (error) => {
      if (isChallengeExpired(error)) {
        expiredByRequest.value = true
        return
      }
      feedback.error(error)
    },
  })

  function resendCode(): void {
    if (resendCooldown.value > 0 || resend.isPending.value) return
    resend.mutate()
  }

  return {
    form,
    method,
    maskedEmail,
    expired,
    errorMessage,
    loginRoute,
    submit,
    resendCode,
    resendCooldown,
    isLoading: challenge.isPending,
    isSubmitting: verify.isPending,
    isResending: resend.isPending,
  }
}
