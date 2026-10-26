import { describe, expect, it } from 'vitest'

import {
  readConsultationCodeDraft,
  readConsultationRequestDraft,
  toConsultationCodeDraft,
  toConsultationRequestDraft,
} from '@/pages/admin/blocked-data/model/open-consultation-form'

describe('readConsultationRequestDraft', () => {
  it('starts blank', () => {
    expect(toConsultationRequestDraft()).toEqual({ authority: '', reference: '', password: '' })
  })

  it('asks for every field of a blank request', () => {
    expect(readConsultationRequestDraft(toConsultationRequestDraft())).toEqual({
      problems: {
        authority: 'pages.admin.blockedData.open.form.problems.authorityRequired',
        reference: 'pages.admin.blockedData.open.form.problems.referenceRequired',
        password: 'pages.admin.blockedData.open.form.problems.passwordRequired',
      },
      value: null,
    })
  })

  it('trims the reference and sends the password as typed', () => {
    expect(
      readConsultationRequestDraft({
        authority: 'DataProtectionAuthority',
        reference: '  E/01234/2026  ',
        password: ' secret ',
      }),
    ).toEqual({
      problems: {},
      value: {
        authority: 'DataProtectionAuthority',
        reference: 'E/01234/2026',
        currentPassword: ' secret ',
      },
    })
  })
})

describe('readConsultationCodeDraft', () => {
  it('starts blank', () => {
    expect(toConsultationCodeDraft()).toEqual({ code: '' })
  })

  it('refuses a code that is not six digits', () => {
    expect(readConsultationCodeDraft({ code: '12345a' })).toEqual({
      problems: { code: 'pages.admin.blockedData.open.form.problems.codeInvalid' },
      value: null,
    })
  })

  it('ignores spaces in the code', () => {
    expect(readConsultationCodeDraft({ code: ' 123 456 ' })).toEqual({
      problems: {},
      value: '123456',
    })
  })
})
