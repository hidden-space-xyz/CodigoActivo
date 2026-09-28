import { ref } from 'vue'
import { describe, expect, it } from 'vitest'

import { usePersonForm } from '@/entities/user'

import { t } from '../../../../support/render'

const TODAY = '2026-07-04'

const storedAdult = {
  firstName: 'Ada',
  lastName: 'Lovelace',
  gender: 'Female',
  email: 'ada@example.test',
  phone: '600000000',
  secondaryPhone: '611111111',
  nationalId: '12345678Z',
  promotionalConsent: false,
  birthDate: '',
  isAdmin: true,
} as const

const storedChild = {
  firstName: 'Tom',
  lastName: 'King',
  gender: 'Male',
  birthDate: '2000-03-04',
} as const

describe('usePersonForm', () => {
  it('loads only the person fields of a record and forgets earlier input', () => {
    const form = usePersonForm({ kind: 'independent', stored: storedAdult, today: TODAY })
    form.currentPassword.value = 'typed'
    form.submit()

    form.load(storedAdult)

    expect({ ...form.draft }).toEqual({
      firstName: 'Ada',
      lastName: 'Lovelace',
      gender: 'Female',
      email: 'ada@example.test',
      phone: '600000000',
      secondaryPhone: '611111111',
      nationalId: '12345678Z',
      promotionalConsent: false,
      birthDate: '',
    })
    expect(form.currentPassword.value).toBe('')
    expect(form.submitted.value).toBe(false)
  })

  it.each([
    ['email', ' ADA@example.test ', false],
    ['email', 'ada@other.test', true],
    ['phone', '600000001', true],
    ['secondaryPhone', ' ', true],
    ['firstName', 'Augusta', false],
    ['nationalId', 'x-1234567-l', false],
  ] as const)(
    'asks for the password when %s becomes %j only if it replaces contact details',
    (field, value, expected) => {
      const form = usePersonForm({ kind: 'independent', stored: storedAdult, today: TODAY })
      form.load(storedAdult)

      form.draft[field] = value

      expect(form.requiresPassword.value).toBe(expected)
    },
  )

  it('never asks for a password while creating an account', () => {
    const form = usePersonForm({ kind: 'independent', stored: null, today: TODAY })

    form.draft.email = 'new@example.test'

    expect(form.requiresPassword.value).toBe(false)
  })

  it('asks for the password once the server refused one, and submits it', () => {
    const form = usePersonForm({ kind: 'independent', stored: storedAdult, today: TODAY })
    form.load(storedAdult)

    form.rejectPassword()

    expect(form.requiresPassword.value).toBe(true)
    expect(form.submit()).toBeNull()
    expect(form.passwordMissing.value).toBe(true)

    form.currentPassword.value = 'secret'

    expect(form.submit()).toEqual({
      kind: 'independent',
      person: {
        firstName: 'Ada',
        lastName: 'Lovelace',
        gender: 'Female',
        email: 'ada@example.test',
        phone: '600000000',
        secondaryPhone: '611111111',
        nationalId: '12345678Z',
        promotionalConsent: false,
      },
      currentPassword: 'secret',
    })
  })

  it('sends no password when the contact details stay the same', () => {
    const form = usePersonForm({ kind: 'independent', stored: storedAdult, today: TODAY })
    form.load(storedAdult)
    form.currentPassword.value = 'typed but not needed'

    expect(form.submit()).toMatchObject({ kind: 'independent', currentPassword: null })
  })

  it('translates the problems only once the form was submitted', () => {
    const form = usePersonForm({ kind: 'independent', stored: null, today: TODAY })

    expect(form.errors.value).toEqual({})
    expect(form.submit()).toBeNull()

    expect(form.errors.value).toEqual({
      firstName: t('entities.user.person.required'),
      lastName: t('entities.user.person.required'),
      gender: t('entities.user.person.genderRequired'),
      email: t('entities.user.person.required'),
      phone: t('entities.user.person.required'),
      nationalId: t('validation.nationalIdFormat'),
    })
  })

  it('lets a dependent who came of age keep the stored birth date but not take another adult one', () => {
    const form = usePersonForm({ kind: 'dependent', stored: storedChild, today: TODAY })
    form.load(storedChild)
    form.rejectPassword()

    expect(form.requiresPassword.value).toBe(false)
    expect(form.submit()).toEqual({
      kind: 'dependent',
      person: { firstName: 'Tom', lastName: 'King', gender: 'Male', birthDate: '2000-03-04' },
      currentPassword: null,
    })

    form.draft.birthDate = '2001-03-04'

    expect(form.submit()).toBeNull()
    expect(form.errors.value).toEqual({ birthDate: t('entities.user.person.birthDateNotMinor') })
  })

  it('switches rules with the kind of the record being edited', () => {
    const kind = ref<'independent' | 'dependent'>('independent')
    const form = usePersonForm({ kind, stored: null, today: TODAY })
    form.load({ firstName: 'Tim', lastName: 'King', gender: 'Male', birthDate: '2016-02-01' })

    expect(form.submit()).toBeNull()

    kind.value = 'dependent'

    expect(form.submit()).toMatchObject({ kind: 'dependent', person: { birthDate: '2016-02-01' } })
  })
})
