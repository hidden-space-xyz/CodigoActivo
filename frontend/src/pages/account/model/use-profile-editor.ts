import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import { isSameEmail, usePersonForm } from '@/entities/user'
import { hasErrorCode } from '@/shared/api'
import { getErrorMessage, useCrudFeedback } from '@/shared/lib/feedback'

import type { useAccount } from './use-account'

/**
 * Dialog that edits the signed-in user's own data. Changes that need the current password ask for
 * it; a wrong password is reported inside the dialog, any other failure with a toast. A new email
 * only replaces the current one once the user opens the link the API emails to it, which the
 * success toast tells them. Call it in `setup`.
 */
export function useProfileEditor(
  account: Pick<ReturnType<typeof useAccount>, 'profile' | 'updateProfile'>,
) {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const user = computed(() => account.profile.data.value ?? null)
  const visible = ref(false)
  const error = ref('')
  const form = usePersonForm({ kind: 'independent', stored: () => user.value })

  function open(): void {
    error.value = ''
    form.load(user.value)
    visible.value = true
  }

  function save(): void {
    error.value = ''
    const submission = form.submit()
    if (submission?.kind !== 'independent') return
    const { email } = submission.person
    const changesEmail = !isSameEmail(email, user.value?.email)
    account.updateProfile.mutate(
      { ...submission.person, currentPassword: submission.currentPassword },
      {
        onSuccess: () => {
          visible.value = false
          if (changesEmail) {
            feedback.success(
              t('pages.account.profile.emailChangeSentDetail', { email }),
              t('pages.account.profile.emailChangeSentSummary'),
            )
            return
          }
          feedback.success(
            t('pages.account.profile.savedDetail'),
            t('pages.account.profile.savedSummary'),
          )
        },
        onError: (failure) => {
          if (hasErrorCode(failure, 'UserCurrentPasswordIncorrect')) {
            form.rejectPassword()
            error.value = getErrorMessage(failure)
            return
          }
          feedback.error(failure)
        },
      },
    )
  }

  return { user, visible, error, form, open, save }
}
