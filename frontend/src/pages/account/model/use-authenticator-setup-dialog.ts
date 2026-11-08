import { computed, ref } from 'vue'

import type { ConfirmationCodeInput } from '@/entities/account'
import { useForm } from '@/shared/lib/form'

import { readTwoFactorCode, readTwoFactorPassword, toTwoFactorDraft } from './two-factor-form'
import type { useTwoFactor } from './use-two-factor'

/** Step the enrollment dialog shows: the password, the emailed code or the key to scan. */
type AuthenticatorSetupStep = 'password' | 'emailCode' | 'code'

function setupCode(currentPassword: string): ConfirmationCodeInput {
  return { currentPassword, purpose: 'AuthenticatorSetup' }
}

/**
 * Dialog that enrolls an authenticator application: the password step emails a code to the current
 * address, the emailed code proves the current second factor and unlocks the shared key to scan,
 * and the code step activates it with the first code the application shows. Each step has its own
 * form, so a code is not refused before the user reaches it, and `submitting` follows the request
 * of the step shown. Call it in `setup`.
 */
export function useAuthenticatorSetupDialog(
  twoFactor: Pick<
    ReturnType<typeof useTwoFactor>,
    'setup' | 'reset' | 'requestSetupCode' | 'beginSetup' | 'confirmSetup'
  >,
) {
  const visible = ref(false)
  const codeSent = ref(false)
  const passwordForm = useForm({ initial: toTwoFactorDraft, read: readTwoFactorPassword })
  const emailCodeForm = useForm({ initial: toTwoFactorDraft, read: readTwoFactorCode })
  const codeForm = useForm({ initial: toTwoFactorDraft, read: readTwoFactorCode })
  const step = computed<AuthenticatorSetupStep>(() => {
    if (twoFactor.setup.value) return 'code'
    return codeSent.value ? 'emailCode' : 'password'
  })
  const submitting = computed(() => {
    if (step.value === 'password') return twoFactor.requestSetupCode.isPending.value
    if (step.value === 'emailCode') return twoFactor.beginSetup.isPending.value
    return twoFactor.confirmSetup.isPending.value
  })

  function open(): void {
    twoFactor.reset()
    codeSent.value = false
    passwordForm.reset()
    emailCodeForm.reset()
    codeForm.reset()
    visible.value = true
  }

  function close(): void {
    visible.value = false
    twoFactor.reset()
  }

  function submitPassword(): void {
    const password = passwordForm.submit()
    if (password === null) return
    twoFactor.requestSetupCode.mutate(setupCode(password), {
      onSuccess: () => {
        codeSent.value = true
      },
    })
  }

  function submitEmailCode(): void {
    const code = emailCodeForm.submit()
    if (code === null) return
    twoFactor.beginSetup.mutate({ currentPassword: passwordForm.draft.password, code })
  }

  function submitAppCode(): void {
    const code = codeForm.submit()
    if (code === null) return
    twoFactor.confirmSetup.mutate(code, {
      onSuccess: () => {
        visible.value = false
      },
    })
  }

  function submit(): void {
    if (step.value === 'password') submitPassword()
    else if (step.value === 'emailCode') submitEmailCode()
    else submitAppCode()
  }

  function resendCode(): void {
    twoFactor.requestSetupCode.mutate(setupCode(passwordForm.draft.password))
  }

  return {
    visible,
    step,
    submitting,
    passwordForm,
    emailCodeForm,
    codeForm,
    open,
    close,
    submit,
    resendCode,
  }
}
