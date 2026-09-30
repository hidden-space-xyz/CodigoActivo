import { flushPromises } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import AdminPasswordDialog from '@/pages/admin/users/ui/AdminPasswordDialog.vue'

import { renderWithProviders, t } from '../../../../../support/render'
import { click, findButton, inputValue, openDialog, typeInto } from '../../../../../support/dom'

const TITLE = 'Restablecer'
const MESSAGE = 'Vas a restablecer la verificación de Ada Lovelace.'
const CONFIRM = 'Restablecer ahora'

async function renderDialog(error = '') {
  const rendered = await renderWithProviders(AdminPasswordDialog, {
    props: {
      visible: false,
      title: TITLE,
      message: MESSAGE,
      confirmLabel: CONFIRM,
      inputId: 'reset-two-factor-password',
      width: 'min(460px, 92vw)',
      saving: false,
      error,
    },
    attach: true,
  })
  await rendered.wrapper.setProps({ visible: true })
  await flushPromises()
  return { ...rendered, dialog: openDialog(TITLE) }
}

describe('AdminPasswordDialog', () => {
  it('explains the action, requires the password and emits it', async () => {
    const { wrapper, dialog } = await renderDialog()

    expect(dialog.textContent).toContain(MESSAGE)
    await click(findButton(CONFIRM, dialog))
    expect(dialog.textContent).toContain(
      t('pages.admin.users.adminPassword.form.problems.passwordRequired'),
    )
    expect(wrapper.emitted('submit')).toBeUndefined()

    await typeInto('#reset-two-factor-password', 'Str0ngPass!23')
    await click(findButton(CONFIRM, dialog))

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
