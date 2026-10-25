import type { FormProblem, FormReading } from '@/shared/lib/form'

import type { BlockedDataAuthority, OpenConsultationInput } from './types'

const CODE_PATTERN = /^\d{6}$/u

/** What the dialog that opens a consultation binds its inputs to. */
export interface OpenConsultationDraft {
  authority: BlockedDataAuthority | ''
  reference: string
  password: string
  code: string
}

/** Fields the dialog that opens a consultation validates. */
type OpenConsultationField = 'authority' | 'reference' | 'password' | 'code'

/** A blank request. */
export function toOpenConsultationDraft(): OpenConsultationDraft {
  return { authority: '', reference: '', password: '', code: '' }
}

/**
 * Reads the dialog that opens a consultation: the authority and the reference of the request are
 * required, the reference is trimmed, the password is sent as typed and the code of the
 * authenticator app has six digits, spaces ignored.
 */
export function readOpenConsultationDraft(
  draft: OpenConsultationDraft,
): FormReading<OpenConsultationField, OpenConsultationInput> {
  const reference = draft.reference.trim()
  const code = draft.code.replace(/\s/gu, '')
  const problems: Partial<Record<OpenConsultationField, FormProblem>> = {}
  if (!draft.authority) {
    problems.authority = 'pages.admin.blockedData.open.form.problems.authorityRequired'
  }
  if (!reference) {
    problems.reference = 'pages.admin.blockedData.open.form.problems.referenceRequired'
  }
  if (!draft.password) {
    problems.password = 'pages.admin.blockedData.open.form.problems.passwordRequired'
  }
  if (!CODE_PATTERN.test(code)) {
    problems.code = 'pages.admin.blockedData.open.form.problems.codeInvalid'
  }
  if (draft.authority === '' || Object.keys(problems).length > 0) {
    return { problems, value: null }
  }
  return {
    problems: {},
    value: { authority: draft.authority, reference, currentPassword: draft.password, code },
  }
}
