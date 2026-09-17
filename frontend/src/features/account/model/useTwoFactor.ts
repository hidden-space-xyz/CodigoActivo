import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMutation, useQueryClient } from '@tanstack/vue-query'

import {
  accountQueryKeys,
  beginAuthenticatorSetupRequest,
  confirmAuthenticatorRequest,
  disableAuthenticatorRequest,
} from '@/entities/account'
import type { AuthenticatorSetup, DisableAuthenticatorInput } from '@/entities/account'
import { getCurrentUserRequest, useSession } from '@/entities/session'
import { TwoFactorMethod } from '@/shared/api/generated/models'
import { getErrorMessage, useCrudFeedback } from '@/shared/lib'

import { toQrCodeDataUrl } from './qr-code'

/**
 * Second-factor settings of the signed-in user. Enrolling an authenticator is a two-step
 * dialog: the password unlocks the shared key (shown as a QR code and in text), and the first
 * code the application generates activates it. Dropping the authenticator needs the password and
 * a current code. Both changes refresh the session user and the cached profile afterwards.
 */
export function useTwoFactor() {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const session = useSession()
  const queryClient = useQueryClient()

  const method = computed(() => session.user?.twoFactorMethod ?? TwoFactorMethod.Email)
  const isAuthenticator = computed(() => method.value === TwoFactorMethod.Authenticator)

  const setup = ref<AuthenticatorSetup | null>(null)
  const qrCodeUrl = ref<string | null>(null)
  const errorMessage = ref('')

  async function refreshUser(): Promise<void> {
    session.setUser(await getCurrentUserRequest())
    await queryClient.invalidateQueries({ queryKey: accountQueryKeys.me() })
  }

  function reset(): void {
    setup.value = null
    qrCodeUrl.value = null
    errorMessage.value = ''
  }

  const beginSetup = useMutation({
    mutationFn: (currentPassword: string) => beginAuthenticatorSetupRequest(currentPassword),
    onSuccess: async (result) => {
      errorMessage.value = ''
      setup.value = result
      qrCodeUrl.value = await toQrCodeDataUrl(result.authenticatorUri)
    },
    onError: (error) => {
      errorMessage.value = getErrorMessage(error)
    },
  })

  const confirmSetup = useMutation({
    mutationFn: (code: string) => confirmAuthenticatorRequest(code),
    onSuccess: async () => {
      reset()
      await refreshUser()
      feedback.success(
        t('features.account.twoFactor.enabledDetail'),
        t('features.account.twoFactor.enabledSummary'),
      )
    },
    onError: (error) => {
      errorMessage.value = getErrorMessage(error)
    },
  })

  const disable = useMutation({
    mutationFn: (input: DisableAuthenticatorInput) => disableAuthenticatorRequest(input),
    onSuccess: async () => {
      reset()
      await refreshUser()
      feedback.success(
        t('features.account.twoFactor.disabledDetail'),
        t('features.account.twoFactor.disabledSummary'),
      )
    },
    onError: (error) => {
      errorMessage.value = getErrorMessage(error)
    },
  })

  return {
    method,
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
