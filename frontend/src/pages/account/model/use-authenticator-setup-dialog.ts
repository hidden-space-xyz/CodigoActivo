import { computed, ref } from 'vue'

import { useForm } from '@/shared/lib/form'

import { readTwoFactorCode, readTwoFactorPassword, toTwoFactorDraft } from './two-factor-form'
import type { useTwoFactor } from './use-two-factor'

/**
 * Dialog that enrolls an authenticator application: the password step unlocks the shared key to
 * scan, and the code step activates it with the first code the application shows. Each step has
 * its own form, so the code is not refused before the user reaches it. Call it in `setup`.
 */
export function useAuthenticatorSetupDialog(
  twoFactor: Pick<
    ReturnType<typeof useTwoFactor>,
    'setup' | 'reset' | 'beginSetup' | 'confirmSetup'
  >,
) {
  const visible = ref(false)
  const passwordForm = useForm({ initial: toTwoFactorDraft, read: readTwoFactorPassword })
  const codeForm = useForm({ initial: toTwoFactorDraft, read: readTwoFactorCode })
  const step = computed(() => (twoFactor.setup.value ? 'code' : 'password'))

  function open(): void {
    twoFactor.reset()
    passwordForm.reset()
    codeForm.reset()
    visible.value = true
  }

  function close(): void {
    visible.value = false
    twoFactor.reset()
  }

  function submit(): void {
    if (step.value === 'password') {
      const password = passwordForm.submit()
      if (password !== null) twoFactor.beginSetup.mutate(password)
      return
    }
    const code = codeForm.submit()
    if (code === null) return
    twoFactor.confirmSetup.mutate(code, {
      onSuccess: () => {
        visible.value = false
      },
    })
  }

  return { visible, step, passwordForm, codeForm, open, close, submit }
}
