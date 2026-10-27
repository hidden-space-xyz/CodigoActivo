import type { DeleteAccountInput } from '@/entities/account'
import type { FormReading } from '@/shared/lib/form'

/** What the account deletion dialog binds its inputs to, across both of its steps. */
export interface DeletionDraft {
  /** Current password, typed in the first step and sent again with the deletion. */
  password: string
  /** Emailed or authenticator code typed in the second step; sent trimmed. */
  code: string
  /** The user accepted that the deletion cannot be undone. */
  accepted: boolean
}

/** Empty draft the dialog opens with: no password, no code and nothing accepted. */
export function toDeletionDraft(): DeletionDraft {
  return { password: '', code: '', accepted: false }
}

/** Reads the first step of the deletion: the password is required and kept as typed. */
export function readDeletionPassword(draft: DeletionDraft): FormReading<'password', string> {
  if (!draft.password) {
    return {
      problems: { password: 'pages.account.deleteAccount.form.problems.passwordRequired' },
      value: null,
    }
  }
  return { problems: {}, value: draft.password }
}

/** The deletion to send once the code is typed and the irreversibility accepted; `null` before. */
export function toDeletionConfirmation(draft: DeletionDraft): DeleteAccountInput | null {
  const code = draft.code.trim()
  if (!draft.password || !code || !draft.accepted) return null
  return { currentPassword: draft.password, code }
}
