import { computed, ref } from 'vue'
import { useMutation } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'

import { accountMutations } from '@/entities/account'
import { useSession } from '@/entities/session'
import { isSameEmail, usePersonForm } from '@/entities/user'
import { hasErrorCode } from '@/shared/api'
import { getErrorMessage, useCrudFeedback } from '@/shared/lib/feedback'

import type { useAccount } from './use-account'

/**
 * Dialog that edits the signed-in user's own data. Changing the email or a phone asks for the
 * current password, and changing the email also for a current code of the second factor: emailed
 * to the current address on demand (`sendCode`) or read from the authenticator application. A
 * wrong password is reported under the password and a wrong or locked code under the code, inside
 * the dialog; any other failure with a toast. A new email only replaces the current one once the
 * user opens the link the API emails to it, which the success toast tells them. Call it in `setup`.
 */
export function useProfileEditor(
  account: Pick<ReturnType<typeof useAccount>, 'profile' | 'updateProfile'>,
) {
  const { t } = useI18n()
  const feedback = useCrudFeedback()
  const session = useSession()
  const user = computed(() => account.profile.data.value ?? null)
  const visible = ref(false)
  const error = ref('')
  const code = ref('')
  const codeError = ref('')
  const codeSent = ref(false)
  const form = usePersonForm({ kind: 'independent', stored: () => user.value })
  const isAuthenticator = computed(() => session.user?.twoFactorMethod === 'Authenticator')
  const requiresCode = computed(
    () => user.value !== null && !isSameEmail(form.draft.email, user.value.email),
  )
  const codeMissing = computed(() => requiresCode.value && !code.value.trim())

  function rejectsCredential(failure: unknown): boolean {
    if (hasErrorCode(failure, 'UserCurrentPasswordIncorrect')) {
      form.rejectPassword()
      error.value = getErrorMessage(failure)
      return true
    }
    if (hasErrorCode(failure, 'TwoFactorCodeInvalid') || hasErrorCode(failure, 'TwoFactorLocked')) {
      codeError.value = getErrorMessage(failure)
      return true
    }
    return false
  }

  const requestCode = useMutation({
    ...accountMutations.requestConfirmationCode(),
    onSuccess: () => {
      codeSent.value = true
    },
    onError: (failure) => {
      if (!rejectsCredential(failure)) codeError.value = getErrorMessage(failure)
    },
  })

  function open(): void {
    error.value = ''
    codeError.value = ''
    code.value = ''
    codeSent.value = false
    form.load(user.value)
    visible.value = true
  }

  function sendCode(): void {
    error.value = ''
    codeError.value = ''
    const currentPassword = form.currentPassword.value
    if (!currentPassword) {
      error.value = t('pages.account.profile.emailChange.passwordFirst')
      return
    }
    requestCode.mutate({ currentPassword, purpose: 'EmailChange' })
  }

  function save(): void {
    error.value = ''
    codeError.value = ''
    const submission = form.submit()
    if (submission?.kind !== 'independent' || codeMissing.value) return
    const { email } = submission.person
    const changesEmail = requiresCode.value
    account.updateProfile.mutate(
      {
        ...submission.person,
        currentPassword: submission.currentPassword,
        code: changesEmail ? code.value.trim() : null,
      },
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
          if (!rejectsCredential(failure)) feedback.error(failure)
        },
      },
    )
  }

  return {
    user,
    visible,
    error,
    form,
    isAuthenticator,
    code,
    codeError,
    codeSent,
    requiresCode,
    codeMissing,
    requestCode,
    open,
    sendCode,
    save,
  }
}
