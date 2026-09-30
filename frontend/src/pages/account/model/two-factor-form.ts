import type { DisableAuthenticatorInput } from '@/entities/account'
import type { FormReading } from '@/shared/lib/form'

/** What the second-factor dialogs bind their inputs to; each step reads only the fields it shows. */
export interface TwoFactorDraft {
  password: string
  code: string
}

/** A blank draft. */
export function toTwoFactorDraft(): TwoFactorDraft {
  return { password: '', code: '' }
}

/** Reads the password that unlocks a second-factor change: it is required and sent as typed. */
export function readTwoFactorPassword(draft: TwoFactorDraft): FormReading<'password', string> {
  if (!draft.password) {
    return {
      problems: { password: 'pages.account.twoFactor.form.problems.passwordRequired' },
      value: null,
    }
  }
  return { problems: {}, value: draft.password }
}

/** Reads a code of the authenticator application: it is required and sent trimmed. */
export function readTwoFactorCode(draft: TwoFactorDraft): FormReading<'code', string> {
  const code = draft.code.trim()
  if (!code) {
    return { problems: { code: 'pages.account.twoFactor.form.problems.codeRequired' }, value: null }
  }
  return { problems: {}, value: code }
}

/** Reads the switch back to emailed codes, which needs both the password and a current code. */
export function readDisableAuthenticator(
  draft: TwoFactorDraft,
): FormReading<keyof TwoFactorDraft, DisableAuthenticatorInput> {
  const password = readTwoFactorPassword(draft)
  const code = readTwoFactorCode(draft)
  if (password.value === null || code.value === null) {
    return { problems: { ...password.problems, ...code.problems }, value: null }
  }
  return { problems: {}, value: { currentPassword: password.value, code: code.value } }
}
