import { reactive, ref } from 'vue'
import { useQueryClient } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'

import { isPasswordTooShort } from '@/entities/account'
import { endSession, logoutRequest } from '@/entities/session'
import { getErrorMessage, useCrudFeedback } from '@/shared/lib/feedback'

import type { useAccount } from './use-account'

/**
 * Dialog that changes the signed-in user's password: the new one must be long enough, differ from
 * the current one and be typed twice alike before it is sent with the current one. The API then
 * closes every session, this one included, so the local session ends too and the user signs in
 * again with the new password. Call it in `setup`.
 */
export function usePasswordChange(account: Pick<ReturnType<typeof useAccount>, 'changePassword'>) {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const queryClient = useQueryClient()
  const router = useRouter()
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

  async function signInAgain(): Promise<void> {
    visible.value = false
    endSession(queryClient)
    await logoutRequest().catch(() => undefined)
    await router.push({ name: 'login' })
    feedback.success(
      t('pages.account.profile.passwordUpdatedDetail'),
      t('pages.account.profile.passwordUpdatedSummary'),
    )
  }

  function save(): void {
    error.value = ''
    if (isPasswordTooShort(form.next)) {
      error.value = t('validation.newPasswordMin')
      return
    }
    if (form.next === form.current) {
      error.value = t('validation.newPasswordSameAsCurrent')
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
          void signInAgain()
        },
        onError: (failure) => {
          error.value = getErrorMessage(failure, t('pages.account.profile.passwordChangeFailed'))
        },
      },
    )
  }

  return { visible, form, error, open, save }
}
