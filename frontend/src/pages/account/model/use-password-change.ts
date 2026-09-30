import { reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { isPasswordTooShort } from '@/entities/account'
import { useCrudFeedback } from '@/shared/lib/feedback'

import type { useAccount } from './use-account'

/**
 * Dialog that changes the signed-in user's password: the new one must be long enough and typed
 * twice alike before it is sent with the current one. Call it in `setup`.
 */
export function usePasswordChange(account: Pick<ReturnType<typeof useAccount>, 'changePassword'>) {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const visible = ref(false)
  const form = reactive({ current: '', next: '', confirm: '' })
  const error = ref('')

  function open(): void {
    form.current = ''
    form.next = ''
    form.confirm = ''
    error.value = ''
    visible.value = true
  }

  function save(): void {
    error.value = ''
    if (isPasswordTooShort(form.next)) {
      error.value = t('validation.newPasswordMin')
      return
    }
    if (form.next !== form.confirm) {
      error.value = t('validation.passwordsMismatch')
      return
    }
    account.changePassword.mutate(
      { currentPassword: form.current, newPassword: form.next },
      {
        onSuccess: () => {
          visible.value = false
          feedback.success(
            t('pages.account.profile.passwordUpdatedDetail'),
            t('pages.account.profile.passwordUpdatedSummary'),
          )
        },
        onError: () => {
          error.value = t('pages.account.profile.passwordChangeFailed')
        },
      },
    )
  }

  return { visible, form, error, open, save }
}
