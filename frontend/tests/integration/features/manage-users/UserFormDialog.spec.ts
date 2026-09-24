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

async function renderDialog(user: User | null, error = '') {
  const rendered = await renderWithProviders(UserFormDialog, {
    props: { visible: false, user, saving: false, error },
    attach: true,
  })
  await rendered.wrapper.setProps({ visible: true })
  await flushPromises()
  return { ...rendered, dialog: openDialog(t(TITLE)) }
}

function consentBox(dialog: HTMLElement): HTMLInputElement {
  const box = dialog.querySelector<HTMLInputElement>('#user-promotional-consent')
  if (!box) throw new Error('No promotional consent box')
  return box
}

const adult = toUser(buildUserResponse())
const minor = toUser(
  buildUserResponse({
    id: 'child-1',
    firstName: 'Tim',
    email: null,
    phone: null,
    birthDate: '2016-02-01',
    nationalId: null,
    gender: 'Male',
    parentId: 'user-1',
    parentName: 'Ada Lovelace',
  }),
)
const dependentWithContact = toUser(
  buildUserResponse({
    id: 'child-2',
    firstName: 'Tim',
    email: 'tim@example.test',
    phone: '622222222',
    secondaryPhone: '633333333',
    birthDate: '2015-05-05',
    nationalId: '87654321X',
    promotionalConsent: true,
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
    expect(dialog.textContent).not.toContain(t('features.manageUsers.dependentContact'))

    await typeInto('#user-first-name', ' Augusta ')
    await typeInto('#user-phone', ' 611111111 ')
    await typeInto('#user-secondary-phone', ' 622222222 ')
    await click(consentBox(dialog))
    await typeInto('#user-current-password', 'admin-password')
    await click(findButton(t('common.save'), dialog))

    expect(wrapper.emitted('submit')?.[0]?.[0]).toEqual({
      firstName: 'Augusta',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phone: '611111111',
      secondaryPhone: '622222222',
      birthDate: null,
      nationalId: '12345678Z',
      promotionalConsent: true,
      gender: 'Female',
      parentId: null,
      currentPassword: 'admin-password',
    })
  })

  it('shows the DNI/NIE twice and the consent of an adult instead of a birth date', async () => {
    const { wrapper, dialog } = await renderDialog(
      toUser(buildUserResponse({ promotionalConsent: true })),
    )

    expect(wrapper.findComponent(ElDatePicker).exists()).toBe(false)
    expect(inputValue('#user-national-id')).toBe('12345678Z')
    expect(inputValue('#user-national-id-confirm')).toBe('12345678Z')
    for (const id of ['#user-national-id', '#user-national-id-confirm']) {
      expect(dialog.querySelector(id)?.getAttribute('autocapitalize')).toBe('characters')
    }
    expect(consentBox(dialog).checked).toBe(true)
    expect(dialog.textContent).toContain(t('common.promotionalConsentOption'))
  })

  it('requires a valid DNI/NIE typed twice and sends it normalized', async () => {
    const { wrapper, dialog } = await renderDialog(adult)

    await typeInto('#user-national-id', 'X1234567A')
    await click(findButton(t('common.save'), dialog))
    expect(dialog.textContent).toContain(t('validation.nationalIdInvalid'))
    expect(dialog.textContent).toContain(t('validation.nationalIdsMismatch'))

    await typeInto('#user-national-id', 'x-1234567-l')
    await click(findButton(t('common.save'), dialog))
    expect(dialog.textContent).not.toContain(t('validation.nationalIdInvalid'))
    expect(dialog.textContent).toContain(t('validation.nationalIdsMismatch'))
    expect(wrapper.emitted('submit')).toBeUndefined()

    await typeInto('#user-national-id-confirm', 'X 1234567 L')
    await click(findButton(t('common.save'), dialog))

    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({
      birthDate: null,
      nationalId: 'X1234567L',
    })
  })

  it('demands the signed-in password before replacing the login identifiers', async () => {
    const { wrapper, dialog } = await renderDialog(adult)

    expect(dialog.querySelector('#user-current-password')).toBeNull()

    await typeInto('#user-email', 'augusta@example.test')
    expect(dialog.querySelector('#user-current-password')).not.toBeNull()
    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('features.manageUsers.contactChange.passwordRequired'))
    expect(wrapper.emitted('submit')).toBeUndefined()

    await typeInto('#user-email', 'ADA@example.test')
    expect(dialog.querySelector('#user-current-password')).toBeNull()
    await click(findButton(t('common.save'), dialog))

    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({
      email: 'ADA@example.test',
      currentPassword: null,
    })
  })

  it('demands the password when the secondary phone changes and refuses one equal to the phone', async () => {
    const { wrapper, dialog } = await renderDialog(
      toUser(buildUserResponse({ secondaryPhone: '622222222' })),
    )

    expect(inputValue('#user-secondary-phone')).toBe('622222222')
    expect(dialog.querySelector('#user-current-password')).toBeNull()
    await typeInto('#user-secondary-phone', ' 600000000 ')
    expect(dialog.querySelector('#user-current-password')).not.toBeNull()
    await typeInto('#user-current-password', 'admin-password')
    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('validation.secondaryPhoneSameAsPrimary'))
    expect(wrapper.emitted('submit')).toBeUndefined()

    await typeInto('#user-secondary-phone', '  ')
    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).not.toContain(t('validation.secondaryPhoneSameAsPrimary'))
    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({
      secondaryPhone: null,
      currentPassword: 'admin-password',
    })
  })

  it('shows the rejection reported by the parent under the password field', async () => {
    const { dialog } = await renderDialog(adult, t('errors.UserCurrentPasswordIncorrect'))

    await typeInto('#user-email', 'augusta@example.test')

    expect(dialog.textContent).toContain(t('errors.UserCurrentPasswordIncorrect'))
  })

  it('reveals the password field when the server refuses the password without a visible change', async () => {
    const { wrapper, dialog } = await renderDialog(adult)

    expect(dialog.querySelector('#user-current-password')).toBeNull()

    await wrapper.setProps({ error: t('errors.UserCurrentPasswordIncorrect') })

    expect(dialog.querySelector('#user-current-password')).not.toBeNull()
    expect(dialog.textContent).toContain(t('errors.UserCurrentPasswordIncorrect'))

    await typeInto('#user-current-password', 'admin-password')
    await wrapper.setProps({ error: '' })
    await click(findButton(t('common.save'), dialog))

    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({
      email: 'ada@example.test',
      currentPassword: 'admin-password',
    })
  })

  it('lets minors omit contact details, DNI/NIE and consent and keeps their guardian', async () => {
    const { wrapper, dialog } = await renderDialog(minor)

    expect(dialog.textContent).toContain(t('features.manageUsers.dependentContact'))
    expect(wrapper.findComponent(ElDatePicker).exists()).toBe(true)
    expect(dialog.querySelector('#user-national-id')).toBeNull()
    expect(dialog.querySelector('#user-promotional-consent')).toBeNull()
    await click(findButton(t('common.save'), dialog))

    expect(wrapper.emitted('submit')?.[0]?.[0]).toEqual({
      firstName: 'Tim',
      lastName: 'Lovelace',
      email: null,
      phone: null,
      secondaryPhone: null,
      birthDate: '2016-02-01',
      nationalId: null,
      promotionalConsent: false,
      gender: 'Male',
      parentId: 'user-1',
      currentPassword: null,
    })
  })

  it('refuses a birth date that turns a dependent into an adult', async () => {
    const { wrapper, dialog } = await renderDialog(minor)
    const picker = wrapper.findComponent(ElDatePicker)

    picker.vm.$emit('update:modelValue', new Date(1999, 4, 5))
    await flushPromises()
    expect(dialog.querySelector('#user-current-password')).toBeNull()

    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('features.manageUsers.childBirthDateNotMinor'))
    expect(wrapper.emitted('submit')).toBeUndefined()

    picker.vm.$emit('update:modelValue', new Date(2017, 2, 3))
    await flushPromises()
    await click(findButton(t('common.save'), dialog))

    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({ birthDate: '2017-03-03' })
  })

  it('keeps a dependent that came of age editable until its birth date changes', async () => {
    const { wrapper, dialog } = await renderDialog(
      toUser(
        buildUserResponse({
          id: 'child-3',
          firstName: 'Tom',
          email: null,
          phone: null,
          birthDate: '2000-03-04',
          nationalId: null,
          gender: 'Male',
          parentId: 'user-1',
          parentName: 'Ada Lovelace',
        }),
      ),
    )
    const picker = wrapper.findComponent(ElDatePicker)

    await typeInto('#user-first-name', 'Thomas')
    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).not.toContain(t('features.manageUsers.childBirthDateNotMinor'))
    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({
      firstName: 'Thomas',
      birthDate: '2000-03-04',
      parentId: 'user-1',
    })

    picker.vm.$emit('update:modelValue', new Date(2001, 2, 4))
    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('features.manageUsers.childBirthDateNotMinor'))
    expect(wrapper.emitted('submit')).toHaveLength(1)

    picker.vm.$emit('update:modelValue', new Date(2012, 6, 8))
    await click(findButton(t('common.save'), dialog))

    expect(wrapper.emitted('submit')?.[1]?.[0]).toMatchObject({ birthDate: '2012-07-08' })
  })

  it('never lets a dependent carry details the server would ignore', async () => {
    const { wrapper, dialog } = await renderDialog(dependentWithContact)

    expect(dialog.querySelector('#user-email')).toBeNull()
    expect(dialog.querySelector('#user-phone')).toBeNull()
    expect(dialog.querySelector('#user-secondary-phone')).toBeNull()
    expect(dialog.textContent).toContain(t('features.manageUsers.dependentContact'))
    expect(dialog.textContent).not.toContain('tim@example.test')

    await click(findButton(t('common.save'), dialog))

    expect(wrapper.emitted('submit')?.[0]?.[0]).toEqual({
      firstName: 'Tim',
      lastName: 'Lovelace',
      email: null,
      phone: null,
      secondaryPhone: null,
      birthDate: '2015-05-05',
      nationalId: null,
      promotionalConsent: false,
      gender: 'Male',
      parentId: 'user-1',
      currentPassword: null,
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

  it('validates names, DNI/NIE and gender for an empty form', async () => {
    const { wrapper, dialog } = await renderDialog(null)

    expect(inputValue('#user-first-name')).toBe('')
    await click(findButton(t('common.save'), dialog))

    expect(dialog.textContent).toContain(t('validation.nationalIdInvalid'))
    expect(dialog.textContent).not.toContain(t('features.manageUsers.birthDateInvalid'))
    expect(dialog.textContent).toContain(t('validation.genderRequired'))
    expect(dialog.querySelectorAll('.ca-invalid').length).toBeGreaterThanOrEqual(4)
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('requires a dependent birth date and rejects future ones, disabled in the picker', async () => {
    const { wrapper, dialog } = await renderDialog(minor)
    const picker = wrapper.findComponent(ElDatePicker)
    const disabledDate = picker.props('disabledDate') as unknown as (date: Date) => boolean

    expect(disabledDate(new Date(2030, 0, 1))).toBe(true)
    expect(disabledDate(new Date(2000, 0, 1))).toBe(false)

    picker.vm.$emit('update:modelValue', null)
    await click(findButton(t('common.save'), dialog))
    expect(dialog.textContent).toContain(t('features.manageUsers.birthDateInvalid'))

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
