import { describe, expect, it } from 'vitest'

import {
  readOpenConsultationDraft,
  toOpenConsultationDraft,
} from '@/pages/admin/blocked-data/model/open-consultation-form'

describe('readOpenConsultationDraft', () => {
  it('starts blank', () => {
    expect(toOpenConsultationDraft()).toEqual({
      authority: '',
      reference: '',
      password: '',
      code: '',
    })
  })

  it('asks for every field of a blank request', () => {
    expect(readOpenConsultationDraft(toOpenConsultationDraft())).toEqual({
      problems: {
        authority: 'pages.admin.blockedData.open.form.problems.authorityRequired',
        reference: 'pages.admin.blockedData.open.form.problems.referenceRequired',
        password: 'pages.admin.blockedData.open.form.problems.passwordRequired',
        code: 'pages.admin.blockedData.open.form.problems.codeInvalid',
      },
      value: null,
    })
  })

  it('refuses a code that is not six digits', () => {
    const reading = readOpenConsultationDraft({
      authority: 'Court',
      reference: 'Ref',
      password: 'secret',
      code: '12345a',
    })

    expect(reading.problems).toEqual({
      code: 'pages.admin.blockedData.open.form.problems.codeInvalid',
    })
    expect(reading.value).toBeNull()
  })

  it('trims the reference, ignores spaces in the code and sends the password as typed', () => {
    expect(
      readOpenConsultationDraft({
        authority: 'DataProtectionAuthority',
        reference: '  E/01234/2026  ',
        password: ' secret ',
        code: '123 456',
      }),
    ).toEqual({
      problems: {},
      value: {
        authority: 'DataProtectionAuthority',
        reference: 'E/01234/2026',
        currentPassword: ' secret ',
        code: '123456',
      },
    })
  })
})
