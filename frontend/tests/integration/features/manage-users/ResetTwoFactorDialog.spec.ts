import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import { toUser } from '@/entities/user/api/mapper'
import { ResetTwoFactorDialog } from '@/features/manage-users'

import { buildUserResponse } from '../../../support/fixtures/user'
import {
  click,
  findButton,
  inputValue,
  openDialog,
  typeInto,
} from '../../../support/fixtures/admin-content/helpers'
import { renderWithProviders, t } from '../../../support/render'

const TITLE = 'features.manageUsers.resetTwoFactor.header'
const ada = toUser(buildUserResponse())

async function renderDialog(error = '') {
  const rendered = await renderWithProviders(ResetTwoFactorDialog, {
    props: { visible: false, user: ada, saving: false, error },
    attach: true,
  })
  await rendered.wrapper.setProps({ visible: true })
  await flushPromises()
  return { ...rendered, dialog: openDialog(t(TITLE)) }
}

describe('ResetTwoFactorDialog', () => {
  it('names the user, requires the password and emits it', async () => {
    const { wrapper, dialog } = await renderDialog()

    expect(dialog.textContent).toContain(
      t('features.manageUsers.resetTwoFactor.message', { fullName: 'Ada Lovelace' }),
    )
    await click(findButton(t('features.manageUsers.resetTwoFactor.confirm'), dialog))
    expect(dialog.textContent).toContain(t('features.manageUsers.resetTwoFactor.passwordRequired'))
    expect(wrapper.emitted('submit')).toBeUndefined()

    await typeInto('#reset-two-factor-password', 'Str0ngPass!23')
    await click(findButton(t('features.manageUsers.resetTwoFactor.confirm'), dialog))

    expect(wrapper.emitted('submit')).toEqual([['Str0ngPass!23']])
  })

  it('shows the parent error, and clears the password whenever it reopens', async () => {
    const { wrapper, dialog } = await renderDialog(t('errors.UserCurrentPasswordIncorrect'))
    expect(dialog.textContent).toContain(t('errors.UserCurrentPasswordIncorrect'))

    await typeInto('#reset-two-factor-password', 'secret')
    await click(findButton(t('common.cancel'), dialog))
    expect(wrapper.emitted('update:visible')).toEqual([[false]])

    await wrapper.setProps({ visible: false })
    await wrapper.setProps({ visible: true })
    await flushPromises()

    expect(inputValue('#reset-two-factor-password')).toBe('')
  })
})
