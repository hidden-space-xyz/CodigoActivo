import { describe, expect, it } from 'vitest'

import { readAdminPasswordDraft } from '@/pages/admin/users/model/admin-password-form'

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
