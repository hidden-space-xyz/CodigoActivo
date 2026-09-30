import { describe, expect, it } from 'vitest'

import {
  readAdminPasswordDraft,
  toAdminPasswordDraft,
} from '@/pages/admin/users/model/admin-password-form'

describe('toAdminPasswordDraft', () => {
  it('starts blank', () => {
    expect(toAdminPasswordDraft()).toEqual({ password: '' })
  })
})

describe('readAdminPasswordDraft', () => {
  it('sends the password as typed', () => {
    expect(readAdminPasswordDraft({ password: ' Str0ngPass!23 ' })).toEqual({
      problems: {},
      value: ' Str0ngPass!23 ',
    })
  })

  it('requires a password', () => {
    expect(readAdminPasswordDraft({ password: '' })).toEqual({
      problems: { password: 'pages.admin.users.adminPassword.form.problems.passwordRequired' },
      value: null,
    })
  })
})
