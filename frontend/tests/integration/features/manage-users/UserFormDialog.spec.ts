import { flushPromises } from '@vue/test-utils'
import { ElDatePicker, ElSelect } from 'element-plus'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { User } from '@/entities/user'
import { toUser } from '@/entities/user/api/mapper'
import { UserFormDialog } from '@/features/manage-users'

import { without } from '../../../support/fixtures/admin-content/builders'
import { buildUserResponse } from '../../../support/fixtures/user'
import {
  click,
  findButton,
  inputValue,
  openDialog,
  typeInto,
} from '../../../support/fixtures/admin-content/helpers'
import { renderWithProviders, t } from '../../../support/render'

const TITLE = 'features.manageUsers.editHeader'

async function renderDialog(user: User | null) {
  const rendered = await renderWithProviders(UserFormDialog, {
    props: { visible: false, user, saving: false },
    attach: true,
  })
  await rendered.wrapper.setProps({ visible: true })
  await flushPromises()
  return { ...rendered, dialog: openDialog(t(TITLE)) }
}

const adult = toUser(buildUserResponse({ birthDate: '1990-05-10' }))
const minor = toUser(
  buildUserResponse({
    id: 'child-1',
    firstName: 'Tim',
    email: null,
    phone: null,
    birthDate: '2016-02-01',
    gender: 'Male',
    parentId: 'user-1',
    parentName: 'Ada Lovelace',
  }),
)

describe('UserFormDialog', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 0, 15, 12))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('populates the form from the user and submits trimmed changes', async () => {
    const { wrapper, dialog } = await renderDialog(adult)

    expect(inputValue('#user-first-name')).toBe('Ada')
    expect(inputValue('#user-email')).toBe('ada@example.test')
    expect(dialog.textContent).not.toContain(t('features.manageUsers.optionalSuffix').trim())

    await typeInto('#user-first-name', ' Augusta ')
    await typeInto('#user-phone', ' 611111111 ')
    await click(findButton(t('common.save'), dialog))

    expect(wrapper.emitted('submit')?.[0]?.[0]).toEqual({
      firstName: 'Augusta',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phone: '611111111',
      birthDate: '1990-05-10',
      gender: 'Female',
      parentId: null,
    })
  })

  it('lets minors omit contact details and keeps their guardian', async () => {
    const { wrapper, dialog } = await renderDialog(minor)

    expect(dialog.textContent).toContain(t('features.manageUsers.optionalSuffix').trim())
    await click(findButton(t('common.save'), dialog))

    expect(wrapper.emitted('submit')?.[0]?.[0]).toEqual({
      firstName: 'Tim',
      lastName: 'Lovelace',
      email: null,
      phone: null,
      birthDate: '2016-02-01',
      gender: 'Male',
      parentId: 'user-1',
    })
  })

  it('requires contact details for adults and a valid email format', async () => {
    const { wrapper, dialog } = await renderDialog(adult)

    await typeInto('#user-phone', '')
    await click(findButton(t('common.save'), dialog))
    expect(dialog.textContent).toContain(t('features.manageUsers.contactRequired'))

    await typeInto('#user-phone', '600000000')
    await typeInto('#user-email', 'not-an-email')
    await click(findButton(t('common.save'), dialog))
    expect(dialog.textContent).toContain(t('validation.emailFormat'))
    expect(dialog.textContent).not.toContain(t('features.manageUsers.contactRequired'))

    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('validates names, birth date and gender for an empty form', async () => {
    const { wrapper, dialog } = await renderDialog(null)

    expect(inputValue('#user-first-name')).toBe('')
    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('features.manageUsers.birthDateInvalid'))
    expect(dialog.textContent).toContain(t('validation.genderRequired'))
    expect(dialog.querySelectorAll('.ca-invalid').length).toBeGreaterThanOrEqual(4)
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('rejects birth dates in the future and disables them in the picker', async () => {
    const { wrapper, dialog } = await renderDialog(adult)
    const picker = wrapper.findComponent(ElDatePicker)
    const disabledDate = picker.props('disabledDate') as unknown as (date: Date) => boolean

    expect(disabledDate(new Date(2030, 0, 1))).toBe(true)
    expect(disabledDate(new Date(2000, 0, 1))).toBe(false)

    picker.vm.$emit('update:modelValue', new Date(2030, 0, 1))
    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('features.manageUsers.birthDateInvalid'))
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('submits the gender picked for a user without one', async () => {
    const { wrapper, dialog } = await renderDialog(toUser(without(buildUserResponse(), 'gender')))

    await click(findButton(t('common.save'), dialog))
    expect(dialog.textContent).toContain(t('validation.genderRequired'))

    wrapper.findComponent(ElSelect).vm.$emit('update:modelValue', 'Other')
    await click(findButton(t('common.save'), dialog))

    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({ gender: 'Other' })
  })

  it('emits a cleared visibility when cancelled', async () => {
    const { wrapper, dialog } = await renderDialog(adult)

    await click(findButton(t('common.cancel'), dialog))

    expect(wrapper.emitted('update:visible')).toEqual([[false]])
  })
})
