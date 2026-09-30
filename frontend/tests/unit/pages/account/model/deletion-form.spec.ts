import { describe, expect, it } from 'vitest'

import {
  readDeletionPassword,
  toDeletionConfirmation,
  toDeletionDraft,
  type DeletionDraft,
} from '@/pages/account/model/deletion-form'

function draftOf(overrides: Partial<DeletionDraft> = {}): DeletionDraft {
  return { password: 'secret', code: ' 123456 ', accepted: true, ...overrides }
}

describe('toDeletionDraft', () => {
  it('starts blank and not accepted', () => {
    expect(toDeletionDraft()).toEqual({ password: '', code: '', accepted: false })
  })
})

describe('readDeletionPassword', () => {
  it('keeps the password as typed', () => {
    expect(readDeletionPassword(draftOf({ password: ' secret ' }))).toEqual({
      problems: {},
      value: ' secret ',
    })
  })

  it('requires a password', () => {
    expect(readDeletionPassword(draftOf({ password: '' }))).toEqual({
      problems: { password: 'pages.account.deleteAccount.form.problems.passwordRequired' },
      value: null,
    })
  })
})

describe('toDeletionConfirmation', () => {
  it('sends the password with the trimmed code once the deletion is accepted', () => {
    expect(toDeletionConfirmation(draftOf())).toEqual({ currentPassword: 'secret', code: '123456' })
  })

  it('waits for the password, a code and the acceptance', () => {
    expect(toDeletionConfirmation(draftOf({ password: '' }))).toBeNull()
    expect(toDeletionConfirmation(draftOf({ code: '   ' }))).toBeNull()
    expect(toDeletionConfirmation(draftOf({ accepted: false }))).toBeNull()
  })
})
