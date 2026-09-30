import { ref } from 'vue'

import { useForm } from '@/shared/lib/form'

import { readDisableAuthenticator, toTwoFactorDraft } from './two-factor-form'
import type { useTwoFactor } from './use-two-factor'

/**
 * Dialog that drops the authenticator application so login codes arrive by email again,
 * authorized by the password and a current code of the application. Call it in `setup`.
 */
export function useDisableAuthenticatorDialog(
  twoFactor: Pick<ReturnType<typeof useTwoFactor>, 'reset' | 'disable'>,
) {
  const visible = ref(false)
  const form = useForm({ initial: toTwoFactorDraft, read: readDisableAuthenticator })

  function open(): void {
    twoFactor.reset()
    form.reset()
    visible.value = true
  }

  function close(): void {
    visible.value = false
    twoFactor.reset()
  }

  function submit(): void {
    const input = form.submit()
    if (!input) return
    twoFactor.disable.mutate(input, {
      onSuccess: () => {
        visible.value = false
      },
    })
  }

  return { visible, form, open, close, submit }
}
