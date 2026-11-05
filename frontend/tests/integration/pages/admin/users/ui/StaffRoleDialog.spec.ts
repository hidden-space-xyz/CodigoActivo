import { flushPromises } from '@vue/test-utils'
import { ElSelect } from 'element-plus'
import { describe, expect, it } from 'vitest'

import { toUser } from '@/entities/user/api/mapper'
import StaffRoleDialog from '@/pages/admin/users/ui/StaffRoleDialog.vue'
import type { StaffRole } from '@/shared/api/generated/models'

import { renderWithProviders, t } from '../../../../../support/render'
import { buildUserResponse } from '../../../../../support/builders'
import { click, findButton, openDialog, typeInto } from '../../../../../support/dom'

const CONFIRM = 'pages.admin.users.staffRole.confirm'

async function renderDialog(staffRole: StaffRole) {
  const rendered = await renderWithProviders(StaffRoleDialog, {
    props: {
      visible: false,
      user: toUser(buildUserResponse({ staffRole })),
      saving: false,
      error: '',
    },
    attach: true,
  })
  await rendered.wrapper.setProps({ visible: true })
  await flushPromises()
  return { ...rendered, dialog: openDialog(t('pages.admin.users.staffRole.header')) }
}

describe('StaffRoleDialog', () => {
  it('preselects the current cargo of the user it names', async () => {
    const { wrapper, dialog } = await renderDialog('Editor')

    expect(dialog.textContent).toContain(
      t('pages.admin.users.staffRole.message', { fullName: 'Ada Lovelace' }),
    )
    expect(wrapper.findComponent(ElSelect).props('modelValue')).toBe('Editor')
    expect(dialog.querySelector('#staff-role-password')).not.toBeNull()
  })

  it('sends a cargo with the password and removes it without one', async () => {
    const { wrapper, dialog } = await renderDialog('None')
    const select = wrapper.findComponent(ElSelect)
    expect(dialog.querySelector('#staff-role-password')).toBeNull()

    select.vm.$emit('update:modelValue', 'Administrator')
    await flushPromises()
    await click(findButton(t(CONFIRM), dialog))
    expect(dialog.textContent).toContain(
      t('pages.admin.users.adminPassword.form.problems.passwordRequired'),
    )
    expect(wrapper.emitted('submit')).toBeUndefined()

    await typeInto('#staff-role-password', 'Str0ngPass!23')
    await click(findButton(t(CONFIRM), dialog))
    select.vm.$emit('update:modelValue', 'None')
    await flushPromises()
    await click(findButton(t(CONFIRM), dialog))

    expect(wrapper.emitted('submit')).toEqual([
      ['Administrator', 'Str0ngPass!23'],
      ['None', null],
    ])
  })
})
