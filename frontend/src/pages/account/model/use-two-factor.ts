import { computed, ref } from 'vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import { accountMutations, type AuthenticatorSetup } from '@/entities/account'
import { refreshSession, useSession } from '@/entities/session'
import { getErrorMessage, useCrudFeedback } from '@/shared/lib/feedback'

import { toQrCodeDataUrl } from '../lib/qr-code'

/**
 * Second-factor settings of the signed-in user. Enrolling an authenticator is a two-step
 * dialog: the password unlocks the shared key (shown as a QR code and in text), and the first
 * code the application generates activates it. Dropping the authenticator needs the password and
 * a current code. Both changes refresh the cached profile and the session user afterwards.
 */
export function useTwoFactor() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const session = useSession()
  const queryClient = useQueryClient()

  const isAuthenticator = computed(() => session.user?.twoFactorMethod === 'Authenticator')
  const setup = ref<AuthenticatorSetup | null>(null)
  const qrCodeUrl = ref<string | null>(null)
  const errorMessage = ref('')

  function reset(): void {
    setup.value = null
    qrCodeUrl.value = null
    errorMessage.value = ''
  }

  function showError(error: unknown): void {
    errorMessage.value = getErrorMessage(error)
  }

  const beginSetup = useMutation({
    ...accountMutations.beginAuthenticatorSetup(),
    onSuccess: async (result) => {
      errorMessage.value = ''
      setup.value = result
      qrCodeUrl.value = await toQrCodeDataUrl(result.authenticatorUri)
    },
    onError: showError,
  })

  const confirmSetup = useMutation({
    ...accountMutations.confirmAuthenticator(),
    onSuccess: async () => {
      reset()
      await refreshSession(queryClient)
      feedback.success(
        t('pages.account.twoFactor.enabledDetail'),
        t('pages.account.twoFactor.enabledSummary'),
      )
    },
    onError: showError,
  })

  const disable = useMutation({
    ...accountMutations.disableAuthenticator(),
    onSuccess: async () => {
      reset()
      await refreshSession(queryClient)
      feedback.success(
        t('pages.account.twoFactor.disabledDetail'),
        t('pages.account.twoFactor.disabledSummary'),
      )
    },
    onError: showError,
  })

  return {
    isAuthenticator,
    setup,
    qrCodeUrl,
    errorMessage,
    reset,
    beginSetup,
    confirmSetup,
    disable,
  }
}
