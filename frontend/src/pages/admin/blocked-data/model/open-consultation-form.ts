import type { FormProblem, FormReading } from '@/shared/lib/form'

import type { BlockedDataAuthority, ConsultationRequest } from './types'

const CODE_PATTERN = /^\d{6}$/u

/** What the first step of the dialog that opens a consultation binds its inputs to. */
export interface ConsultationRequestDraft {
  authority: BlockedDataAuthority | ''
  reference: string
  password: string
}

/** Fields the first step of the dialog validates. */
type ConsultationRequestField = 'authority' | 'reference' | 'password'

/** What the second step of the dialog binds its input to. */
export interface ConsultationCodeDraft {
  code: string
}

/** A blank request. */
export function toConsultationRequestDraft(): ConsultationRequestDraft {
  return { authority: '', reference: '', password: '' }
}

/** A blank code. */
export function toConsultationCodeDraft(): ConsultationCodeDraft {
  return { code: '' }
}

/**
 * Reads the first step of the dialog that opens a consultation: the authority and the reference
 * of the request are required, the reference is trimmed and the password is sent as typed.
 */
export function readConsultationRequestDraft(
  draft: ConsultationRequestDraft,
): FormReading<ConsultationRequestField, ConsultationRequest> {
  const reference = draft.reference.trim()
  const problems: Partial<Record<ConsultationRequestField, FormProblem>> = {}
  if (!draft.authority) {
    problems.authority = 'pages.admin.blockedData.open.form.problems.authorityRequired'
  }
  if (!reference) {
    problems.reference = 'pages.admin.blockedData.open.form.problems.referenceRequired'
  }
  if (!draft.password) {
    problems.password = 'pages.admin.blockedData.open.form.problems.passwordRequired'
  }
  if (draft.authority === '' || Object.keys(problems).length > 0) {
    return { problems, value: null }
  }
  return {
    problems: {},
    value: { authority: draft.authority, reference, currentPassword: draft.password },
  }
}

/** Reads the second step: the code of the second factor has six digits, spaces ignored. */
export function readConsultationCodeDraft(
  draft: ConsultationCodeDraft,
): FormReading<'code', string> {
  const code = draft.code.replace(/\s/gu, '')
  if (!CODE_PATTERN.test(code)) {
    return {
      problems: { code: 'pages.admin.blockedData.open.form.problems.codeInvalid' },
      value: null,
    }
  }
  return { problems: {}, value: code }
}
