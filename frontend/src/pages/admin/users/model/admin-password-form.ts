import type { FormReading } from '@/shared/lib/form'

/** What the admin password dialog binds its input to. */
export interface AdminPasswordDraft {
  password: string
}

/** A blank password. */
export function toAdminPasswordDraft(): AdminPasswordDraft {
  return { password: '' }
}

/** Reads the admin password dialog: the password is required and sent as typed. */
export function readAdminPasswordDraft(draft: AdminPasswordDraft): FormReading<'password', string> {
  if (!draft.password) {
    return {
      problems: { password: 'pages.admin.users.adminPassword.form.problems.passwordRequired' },
      value: null,
    }
  }
  return { problems: {}, value: draft.password }
}
