import { describe, expect, it } from 'vitest'

import {
  createEmptyMinor,
  createEmptyRegistrationForm,
} from '@/features/register/model/registration-form'

describe('registration form factories', () => {
  it('creates a blank form without minors', () => {
    expect(createEmptyRegistrationForm()).toEqual({
      firstName: '',
      lastName: '',
      email: '',
      phone: '',
      secondaryPhone: '',
      password: '',
      confirmPassword: '',
      nationalId: '',
      confirmNationalId: '',
      gender: null,
      promotionalConsent: false,
      minors: [],
    })
  })

  it('returns independent forms on every call', () => {
    const first = createEmptyRegistrationForm()
    first.minors.push(createEmptyMinor())

    expect(createEmptyRegistrationForm().minors).toEqual([])
  })

  it('creates blank minors with increasing unique keys', () => {
    const first = createEmptyMinor()
    const second = createEmptyMinor()

    expect(first).toEqual({
      key: first.key,
      firstName: '',
      lastName: '',
      dateOfBirth: '',
      gender: null,
    })
    expect(second.key).toBeGreaterThan(first.key)
  })
})
