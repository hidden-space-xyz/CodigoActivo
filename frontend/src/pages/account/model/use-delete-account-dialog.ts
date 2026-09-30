import { computed, ref } from 'vue'

import { useForm } from '@/shared/lib/form'

import { readDeletionPassword, toDeletionConfirmation, toDeletionDraft } from './deletion-form'
import type { useDeleteAccount } from './use-delete-account'

/**
 * Dialog of the account deletion: the password step asks for the password and, unless the user
 * reads codes from an authenticator application, emails a code; the code step sends the deletion
 * once the code is typed and the irreversibility accepted. Call it in `setup`.
 */
export function useDeleteAccountDialog(
  deletion: Pick<
    ReturnType<typeof useDeleteAccount>,
    'isAuthenticator' | 'reset' | 'requestCode' | 'confirm'
  >,
) {
  const visible = ref(false)
  const codeStep = ref(false)
  const form = useForm({ initial: toDeletionDraft, read: readDeletionPassword })
  const confirmation = computed(() => toDeletionConfirmation(form.draft))

  function open(): void {
    deletion.reset()
    codeStep.value = false
    form.reset()
    visible.value = true
  }

  function close(): void {
    visible.value = false
    deletion.reset()
  }

  function submitPassword(): void {
    const password = form.submit()
    if (!password) return
    if (deletion.isAuthenticator.value) {
      codeStep.value = true
      return
    }
    deletion.requestCode.mutate(password, {
      onSuccess: () => {
        codeStep.value = true
      },
    })
  }

  function resendCode(): void {
    deletion.requestCode.mutate(form.draft.password)
  }

  function submitDeletion(): void {
    const input = confirmation.value
    if (!input) return
    deletion.confirm.mutate(input, {
      onSuccess: () => {
        visible.value = false
      },
    })
  }

  function submit(): void {
    if (codeStep.value) submitDeletion()
    else submitPassword()
  }

  return {
    visible,
    codeStep,
    form,
    canConfirm: computed(() => confirmation.value !== null),
    open,
    close,
    submit,
    resendCode,
  }
}
