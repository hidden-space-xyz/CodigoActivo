import { computed, onScopeDispose, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import type { RouteLocationRaw } from 'vue-router'
import { useMutation, useQuery } from '@tanstack/vue-query'

import {
  getLoginChallengeRequest,
  resendTwoFactorCodeRequest,
  sessionQueryKeys,
  useSession,
  verifyTwoFactorLoginRequest,
} from '@/entities/session'
import { ApiError } from '@/shared/api'
import { TwoFactorMethod } from '@/shared/api/generated/models'
import { getErrorMessage, toLocalRedirect, useCrudFeedback } from '@/shared/lib'

const RESEND_COOLDOWN_SECONDS = 60

function isChallengeExpired(error: unknown): boolean {
  return error instanceof ApiError && error.code === 'TwoFactorChallengeExpired'
}

/**
 * Second step of the login. Loads the pending challenge from the API (the browser holds it in a
 * cookie, so a reload survives), verifies the typed code and then stores the user in the session
 * and navigates to the `redirect` query parameter or home. Emailed codes can be resent after a
 * 60-second cooldown, which also runs when the page opens because a code was just sent. An
 * expired challenge sends the user back to the login page.
 */
export function useTwoFactorLogin() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const session = useSession()
  const router = useRouter()
  const route = useRoute()

  const form = reactive({ code: '' })
  const errorMessage = ref<string | null>(null)
  const expiredByRequest = ref(false)
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

  const redirect = computed(() => toLocalRedirect(route.query.redirect))

  const loginRoute = computed<RouteLocationRaw>(() => ({
    name: 'login',
    ...(redirect.value ? { query: { redirect: redirect.value } } : {}),
  }))

  const challenge = useQuery({
    queryKey: sessionQueryKeys.loginChallenge(),
    queryFn: () => getLoginChallengeRequest(),
    staleTime: 0,
    gcTime: 0,
  })

  watch(
    () => challenge.data.value,
    (value) => {
      if (value?.method === TwoFactorMethod.Email) startCooldown()
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
    mutationFn: (code: string) => verifyTwoFactorLoginRequest(code),
    onSuccess: (user) => {
      session.setUser(user)
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
    verify.mutate(code)
  }

  const resend = useMutation({
    mutationFn: () => resendTwoFactorCodeRequest(),
    onSuccess: () => {
      startCooldown()
      feedback.success(
        t('features.auth.twoFactor.codeResentDetail'),
        t('features.auth.twoFactor.codeResentSummary'),
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
