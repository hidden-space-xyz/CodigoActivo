import { reactive } from 'vue'
import type { VueWrapper } from '@vue/test-utils'
import { ElSelect } from 'element-plus'
import { describe, expect, it, vi } from 'vitest'

import RegistrationForm from '@/pages/register/ui/RegistrationForm.vue'
import {
  createEmptyRegistrationForm,
  type RegistrationForm as RegistrationFormModel,
} from '@/pages/register/model/registration-form'
import type { Gender } from '@/shared/api/generated/models'
import { PASSWORD_MIN_LENGTH } from '@/shared/config'

import { renderWithProviders, t } from '../../../../support/render'
import { TEST_LEGAL_ENTITY } from '../../../../support/server'

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
  await selectGender(wrapper, 0, 'Female')
}

function addMinorButton(wrapper: VueWrapper) {
  const button = wrapper
    .findAll('button')
    .find((candidate) => candidate.text() === t('pages.register.form.addMinor'))
  if (!button) throw new Error('add minor button not found')
  return button
}

function errors(wrapper: VueWrapper): string[] {
  return wrapper.findAll('.ca-field__error').map((error) => error.text())
}

describe('RegistrationForm', () => {
  it('groups the adult into personal data, contact and account access, then minors and consent', async () => {
    const { wrapper } = await renderForm()

    expect(wrapper.findAll('h2').map((heading) => heading.text())).toEqual([
      t('pages.register.form.yourData'),
      t('pages.register.form.contactTitle'),
      t('pages.register.form.accessTitle'),
      t('pages.register.form.minorsTitle'),
      t('pages.register.form.privacy.title'),
    ])
    expect(wrapper.findAll('[id^="reg-"]').map((field) => field.attributes('id'))).toEqual([
      'reg-firstname',
      'reg-lastname',
      'reg-national-id',
      'reg-gender',
      'reg-phone',
      'reg-secondary-phone',
      'reg-email',
      'reg-password',
      'reg-password-confirm',
      'reg-news-consent',
      'reg-privacy-title',
    ])
    expect(wrapper.find('#reg-national-id-confirm').exists()).toBe(false)
  })

  it('gives the basic data protection information before submitting, linking to the full policy', async () => {
    const { wrapper } = await renderForm()

    const notice = wrapper.get('.reg-privacy')
    expect(notice.attributes('aria-labelledby')).toBe('reg-privacy-title')
    expect(notice.findAll('dt').map((term) => term.text())).toEqual([
      t('pages.register.form.privacy.controller'),
      t('pages.register.form.privacy.purpose'),
      t('pages.register.form.privacy.legalBasis'),
      t('pages.register.form.privacy.recipients'),
      t('pages.register.form.privacy.rights'),
      t('pages.register.form.privacy.more'),
    ])
    await vi.waitFor(() => expect(notice.text()).toContain(TEST_LEGAL_ENTITY.name))
    const link = notice.get('a')
    expect(link.attributes('href')).toBe('/privacy')
    expect(link.attributes('target')).toBe('_blank')
    expect(link.attributes('rel')).toBe('noopener')
    expect(wrapper.get('.reg__footer').element.contains(notice.element)).toBe(true)
  })

  it('shows every validation error on an empty submit and does not emit', async () => {
    const { wrapper } = await renderForm()

    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(errors(wrapper)).toEqual([
      t('entities.user.person.required'),
      t('entities.user.person.required'),
      t('validation.nationalIdFormat'),
      t('entities.user.person.genderRequired'),
      t('entities.user.person.required'),
      t('entities.user.person.required'),
      t('validation.passwordMin', { min: PASSWORD_MIN_LENGTH }),
    ])
    expect(wrapper.findAll('.ca-invalid').length).toBeGreaterThanOrEqual(6)
  })

  it('links every reported problem to its field so screen readers announce it', async () => {
    const { wrapper } = await renderForm()
    await addMinorButton(wrapper).trigger('click')
    const ids = [
      'reg-firstname',
      'reg-lastname',
      'reg-national-id',
      'reg-phone',
      'reg-email',
      'reg-password',
      'minor-firstname-0',
      'minor-lastname-0',
      'minor-dob-0',
    ]
    for (const id of ids) {
      expect(wrapper.get(`#${id}`).attributes('aria-invalid')).toBe('false')
      expect(wrapper.get(`#${id}`).attributes('aria-describedby')).toBeUndefined()
    }

    await wrapper.find('form').trigger('submit')

    for (const id of ids) {
      expect(wrapper.get(`#${id}`).attributes('aria-invalid')).toBe('true')
      expect(wrapper.get(`#${id}`).attributes('aria-describedby')).toBe(`${id}-error`)
      expect(wrapper.get(`#${id}-error`).classes()).toContain('ca-field__error')
    }
  })

  it('does not show validation errors while a field is being typed', async () => {
    const { wrapper } = await renderForm()

    await wrapper.find('#reg-email').setValue('not-an-email')

    expect(errors(wrapper)).toEqual([])
    expect(wrapper.find('.ca-invalid').exists()).toBe(false)
  })

  it('reports a wrong value as soon as its field is left, but not a blank one', async () => {
    const { wrapper } = await renderForm()
    for (const id of ['#reg-national-id', '#reg-secondary-phone', '#reg-email', '#reg-password']) {
      await wrapper.find(id).trigger('blur')
    }
    expect(errors(wrapper)).toEqual([])

    await wrapper.find('#reg-phone').setValue('600000000')
    for (const [id, value] of [
      ['#reg-national-id', '1234567'],
      ['#reg-secondary-phone', '600000000'],
      ['#reg-email', 'ada@example'],
      ['#reg-password', 'short'],
    ] as const) {
      await wrapper.find(id).setValue(value)
      await wrapper.find(id).trigger('blur')
    }

    expect(errors(wrapper)).toEqual([
      t('validation.nationalIdFormat'),
      t('entities.user.person.sameAsPhone'),
      t('entities.user.person.emailFormat'),
      t('validation.passwordMin', { min: PASSWORD_MIN_LENGTH }),
    ])

    await wrapper.find('#reg-email').setValue('ada@example.test')
    await wrapper.find('#reg-password').setValue('correct-horse-battery')

    expect(errors(wrapper)).toEqual([
      t('validation.nationalIdFormat'),
      t('entities.user.person.sameAsPhone'),
    ])
    expect(wrapper.emitted('submit')).toBeUndefined()
  })

  it('refuses a secondary phone equal to the phone', async () => {
    const { form, wrapper } = await renderForm()
    await fillAdult(wrapper)
    await wrapper.find('#reg-secondary-phone').setValue(' 600000000 ')

    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(errors(wrapper)).toEqual([t('entities.user.person.sameAsPhone')])
    expect(wrapper.find('#reg-secondary-phone').element.closest('.ca-invalid')).not.toBeNull()

    await wrapper.find('#reg-secondary-phone').setValue('611111111')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toHaveLength(1)
    expect(form.secondaryPhone).toBe('611111111')
  })

  it('reports mismatched passwords once the confirmation loses focus', async () => {
    const { wrapper } = await renderForm()
    await wrapper.find('#reg-password').setValue('correct-horse-battery')
    await wrapper.find('#reg-password-confirm').setValue('correct-horse-batter')
    expect(errors(wrapper)).toEqual([])

    await wrapper.find('#reg-password-confirm').trigger('blur')

    expect(errors(wrapper)).toEqual([t('validation.passwordsMismatch')])
  })

  it('checks the DNI/NIE control letter once the field is left and normalizes it', async () => {
    const { form, wrapper } = await renderForm()
    await wrapper.find('#reg-national-id').setValue('12345678X')
    expect(errors(wrapper)).toEqual([])

    await wrapper.find('#reg-national-id').trigger('blur')
    expect(errors(wrapper)).toEqual([t('validation.nationalIdLetter')])

    await wrapper.find('#reg-national-id').setValue(' 1234 5678-z ')
    expect(errors(wrapper)).toEqual([])

    await wrapper.find('#reg-national-id').trigger('blur')
    expect(form.nationalId).toBe('12345678Z')
  })

  it('does not emit with a DNI/NIE whose control letter does not match', async () => {
    const { wrapper } = await renderForm()
    await fillAdult(wrapper)
    await wrapper.find('#reg-national-id').setValue('X1234567A')

    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(errors(wrapper)).toEqual([t('validation.nationalIdLetter')])
  })

  it('offers an unchecked news consent box that updates the form', async () => {
    const { form, wrapper } = await renderForm()
    const consent = wrapper.get('#reg-news-consent')

    expect(wrapper.text()).toContain(t('common.newsConsentOption'))
    expect((consent.element as HTMLInputElement).checked).toBe(false)

    await consent.setValue(true)

    expect(form.newsConsent).toBe(true)
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
      gender: 'Female',
      newsConsent: false,
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
      t('pages.register.form.minorLegend', { n: 1 }),
      t('pages.register.form.minorLegend', { n: 2 }),
    ])
    expect(wrapper.get('.reg__minors').element.lastElementChild).toBe(
      addMinorButton(wrapper).element,
    )

    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(errors(wrapper)).toEqual(
      Array.from({ length: 2 }, () => [
        t('entities.user.person.required'),
        t('entities.user.person.required'),
        t('entities.user.person.birthDateInvalid'),
        t('entities.user.person.genderRequired'),
      ]).flat(),
    )

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
        birthDate: '2016-02-03',
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

  it('asks no birth date of the adult and limits minors to those not yet 18 today', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-09-17T12:00:00Z'))
    const { wrapper } = await renderForm()

    await addMinorButton(wrapper).trigger('click')

    expect(wrapper.find('#reg-dob').exists()).toBe(false)
    expect(wrapper.get('#minor-dob-0').attributes('min')).toBe('2008-09-18')
    expect(wrapper.get('#minor-dob-0').attributes('max')).toBe('2026-09-17')
  })

  it('refuses a minor who turns 18 today, as the server would', async () => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-09-17T12:00:00Z'))
    const { wrapper } = await renderForm()
    await fillAdult(wrapper)
    await addMinorButton(wrapper).trigger('click')
    await wrapper.find('#minor-firstname-0').setValue('Byron')
    await wrapper.find('#minor-lastname-0').setValue('King')
    await selectGender(wrapper, 1, 'Male')
    await wrapper.find('#minor-dob-0').setValue('2008-09-17')

    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(errors(wrapper)).toEqual([t('entities.user.person.birthDateNotMinor')])

    await wrapper.find('#minor-dob-0').setValue('2008-09-18')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toHaveLength(1)
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
