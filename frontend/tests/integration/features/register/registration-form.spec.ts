import { reactive } from 'vue'
import type { VueWrapper } from '@vue/test-utils'
import { ElSelect } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import { RegistrationForm } from '@/features/register'
import {
  createEmptyRegistrationForm,
  type RegistrationForm as RegistrationFormModel,
} from '@/features/register/model/registration-form'
import type { Gender } from '@/shared/api/generated/models'

import { renderWithProviders, t } from '../../../support/render'

async function renderForm(isSubmitting = false) {
  const form = reactive<RegistrationFormModel>(createEmptyRegistrationForm())
  const { wrapper } = await renderWithProviders(RegistrationForm, {
    props: { form, isSubmitting },
  })
  return { form, wrapper }
}

async function selectGender(wrapper: VueWrapper, index: number, gender: Gender): Promise<void> {
  const select = wrapper.findAllComponents(ElSelect)[index]
  if (!select) throw new Error(`select ${index} not found`)
  select.vm.$emit('update:modelValue', gender)
  await wrapper.vm.$nextTick()
}

async function fillAdult(wrapper: VueWrapper): Promise<void> {
  await wrapper.find('#reg-firstname').setValue(' Ada ')
  await wrapper.find('#reg-lastname').setValue('Lovelace')
  await wrapper.find('#reg-email').setValue('ada@example.test')
  await wrapper.find('#reg-phone').setValue('600000000')
  await wrapper.find('#reg-password').setValue('correct-horse-battery')
  await wrapper.find('#reg-password-confirm').setValue('correct-horse-battery')
  await wrapper.find('#reg-national-id').setValue('x-1234567-l')
  await wrapper.find('#reg-national-id-confirm').setValue('X1234567L')
  await selectGender(wrapper, 0, 'Female')
}

function addMinorButton(wrapper: VueWrapper) {
  const button = wrapper
    .findAll('button')
    .find((candidate) => candidate.text() === t('features.register.form.addMinor'))
  if (!button) throw new Error('add minor button not found')
  return button
}

function errors(wrapper: VueWrapper): string[] {
  return wrapper.findAll('.reg__error').map((error) => error.text())
}

describe('RegistrationForm', () => {
  it('shows every validation error on an empty submit and does not emit', async () => {
    const { wrapper } = await renderForm()

    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(errors(wrapper)).toEqual([
      t('validation.emailInvalid'),
      t('validation.passwordMin'),
      t('validation.nationalIdInvalid'),
      t('validation.genderRequired'),
    ])
    expect(wrapper.findAll('.ca-invalid').length).toBeGreaterThanOrEqual(6)
  })

  it('does not show validation errors before the first submit', async () => {
    const { wrapper } = await renderForm()

    await wrapper.find('#reg-email').setValue('not-an-email')

    expect(errors(wrapper)).toEqual([])
    expect(wrapper.find('.ca-invalid').exists()).toBe(false)
  })

  it('reports mismatched passwords once the confirmation loses focus', async () => {
    const { wrapper } = await renderForm()
    await wrapper.find('#reg-password').setValue('correct-horse-battery')
    await wrapper.find('#reg-password-confirm').setValue('correct-horse-batter')
    expect(errors(wrapper)).toEqual([])

    await wrapper.find('#reg-password-confirm').trigger('blur')

    expect(errors(wrapper)).toEqual([t('validation.passwordsMismatch')])
  })

  it('reports mismatched DNI/NIE entries once the confirmation loses focus', async () => {
    const { wrapper } = await renderForm()
    await wrapper.find('#reg-national-id').setValue('12345678Z')
    await wrapper.find('#reg-national-id-confirm').setValue('12345678X')
    expect(errors(wrapper)).toEqual([])

    await wrapper.find('#reg-national-id-confirm').trigger('blur')
    expect(errors(wrapper)).toEqual([t('validation.nationalIdsMismatch')])

    await wrapper.find('#reg-national-id-confirm').setValue(' 1234 5678-z ')
    expect(errors(wrapper)).toEqual([])
  })

  it('rejects a DNI/NIE with a wrong control letter even when both entries match', async () => {
    const { wrapper } = await renderForm()
    await fillAdult(wrapper)
    await wrapper.find('#reg-national-id').setValue('X1234567A')
    await wrapper.find('#reg-national-id-confirm').setValue('X1234567A')

    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(errors(wrapper)).toEqual([t('validation.nationalIdInvalid')])
  })

  it('offers an unchecked promotional consent box that updates the form', async () => {
    const { form, wrapper } = await renderForm()
    const consent = wrapper.get('#reg-promotional-consent')

    expect(wrapper.text()).toContain(t('common.promotionalConsentOption'))
    expect((consent.element as HTMLInputElement).checked).toBe(false)

    await consent.setValue(true)

    expect(form.promotionalConsent).toBe(true)
  })

  it('emits submit once every adult field is valid, keeping the data in the shared form', async () => {
    const { form, wrapper } = await renderForm()
    await fillAdult(wrapper)

    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toHaveLength(1)
    expect(errors(wrapper)).toEqual([])
    expect(form).toMatchObject({
      firstName: ' Ada ',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phone: '600000000',
      password: 'correct-horse-battery',
      confirmPassword: 'correct-horse-battery',
      nationalId: 'x-1234567-l',
      confirmNationalId: 'X1234567L',
      gender: 'Female',
      promotionalConsent: false,
      minors: [],
    })
  })

  it.each([
    ['#reg-firstname', '   '],
    ['#reg-lastname', ''],
    ['#reg-email', 'ada@example'],
    ['#reg-phone', ' '],
    ['#reg-password', 'short'],
    ['#reg-password-confirm', 'different-password'],
    ['#reg-national-id', 'X1234567A'],
    ['#reg-national-id-confirm', '12345678Z'],
  ])('does not emit when %s is set to %j', async (selector, value) => {
    const { wrapper } = await renderForm()
    await fillAdult(wrapper)
    await wrapper.find(selector).setValue(value)

    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('adds minors that must be completed before submitting, and removes them', async () => {
    const { form, wrapper } = await renderForm()
    await fillAdult(wrapper)

    await addMinorButton(wrapper).trigger('click')
    await addMinorButton(wrapper).trigger('click')
    expect(wrapper.findAll('legend').map((legend) => legend.text())).toEqual([
      t('features.register.form.minorLegend', { n: 1 }),
      t('features.register.form.minorLegend', { n: 2 }),
    ])

    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(errors(wrapper)).toEqual([
      t('validation.genderRequired'),
      t('validation.genderRequired'),
    ])

    await wrapper.find('#minor-firstname-0').setValue('Byron')
    await wrapper.find('#minor-lastname-0').setValue('King')
    await wrapper.find('#minor-dob-0').setValue('2016-02-03')
    await selectGender(wrapper, 1, 'Male')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('submit')).toBeUndefined()

    await wrapper.findAll('.reg__minor-remove')[1]?.trigger('click')
    expect(wrapper.findAll('fieldset')).toHaveLength(1)

    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('submit')).toHaveLength(1)
    expect(form.minors).toEqual([
      {
        key: form.minors[0]?.key,
        firstName: 'Byron',
        lastName: 'King',
        dateOfBirth: '2016-02-03',
        gender: 'Male',
      },
    ])
  })

  it.each([
    ['#minor-firstname-0', ''],
    ['#minor-lastname-0', ' '],
    ['#minor-dob-0', ''],
  ])('does not emit when the minor field %s is set to %j', async (selector, value) => {
    const { wrapper } = await renderForm()
    await fillAdult(wrapper)
    await addMinorButton(wrapper).trigger('click')
    await wrapper.find('#minor-firstname-0').setValue('Byron')
    await wrapper.find('#minor-lastname-0').setValue('King')
    await wrapper.find('#minor-dob-0').setValue('2016-02-03')
    await selectGender(wrapper, 1, 'Male')
    await wrapper.find(selector).setValue(value)

    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('asks no birth date of the adult and limits minors to between 18 years ago and today', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-09-17T12:00:00Z'))
    const { wrapper } = await renderForm()

    await addMinorButton(wrapper).trigger('click')

    expect(wrapper.find('#reg-dob').exists()).toBe(false)
    expect(wrapper.get('#minor-dob-0').attributes('min')).toBe('2008-09-17')
    expect(wrapper.get('#minor-dob-0').attributes('max')).toBe('2026-09-17')
  })

  it('emits back from the back link', async () => {
    const { wrapper } = await renderForm()

    await wrapper.get('.reg__head button').trigger('click')

    expect(wrapper.emitted('back')).toHaveLength(1)
  })

  it('shows a loading submit button while submitting', async () => {
    const { wrapper } = await renderForm(true)

    const submit = wrapper.get('button[type="submit"]')
    expect(submit.attributes('aria-busy')).toBe('true')
    expect(submit.attributes('disabled')).toBeDefined()
  })
})
