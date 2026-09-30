import { describe, expect, it } from 'vitest'

import {
  readDisableAuthenticator,
  readTwoFactorCode,
  readTwoFactorPassword,
  toTwoFactorDraft,
} from '@/pages/account/model/two-factor-form'

const PASSWORD_REQUIRED = 'pages.account.twoFactor.form.problems.passwordRequired'
const CODE_REQUIRED = 'pages.account.twoFactor.form.problems.codeRequired'

describe('toTwoFactorDraft', () => {
  it('starts blank', () => {
    expect(toTwoFactorDraft()).toEqual({ password: '', code: '' })
  })
})

describe('readTwoFactorPassword', () => {
  it('sends the password as typed and ignores the code', () => {
    expect(readTwoFactorPassword({ password: ' secret ', code: '' })).toEqual({
      problems: {},
      value: ' secret ',
    })
  })

  it('requires a password', () => {
    expect(readTwoFactorPassword({ password: '', code: '123456' })).toEqual({
      problems: { password: PASSWORD_REQUIRED },
      value: null,
    })
  })
})

describe('readTwoFactorCode', () => {
  it('sends the code trimmed and ignores the password', () => {
    expect(readTwoFactorCode({ password: '', code: ' 123456 ' })).toEqual({
      problems: {},
      value: '123456',
    })
  })

  it('requires a code that is not blank', () => {
    expect(readTwoFactorCode({ password: 'secret', code: '   ' })).toEqual({
      problems: { code: CODE_REQUIRED },
      value: null,
    })
  })
})

describe('readDisableAuthenticator', () => {
  it('sends the password as typed with the trimmed code', () => {
    expect(readDisableAuthenticator({ password: 'secret', code: ' 654321 ' })).toEqual({
      problems: {},
      value: { currentPassword: 'secret', code: '654321' },
    })
  })

  it('refuses each missing field', () => {
    expect(readDisableAuthenticator({ password: '', code: '' })).toEqual({
      problems: { password: PASSWORD_REQUIRED, code: CODE_REQUIRED },
      value: null,
    })
    expect(readDisableAuthenticator({ password: 'secret', code: '' })).toEqual({
      problems: { code: CODE_REQUIRED },
      value: null,
    })
    expect(readDisableAuthenticator({ password: '', code: '654321' })).toEqual({
      problems: { password: PASSWORD_REQUIRED },
      value: null,
    })
  })
})
