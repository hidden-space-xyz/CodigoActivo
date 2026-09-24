import { describe, expect, it } from 'vitest'

import { toRegisterRequest, toRegistrationResult } from '@/features/register/api/mapper'
import type { RegistrationForm } from '@/features/register/model/registration-form'

import { buildUserResponse } from '../../../../support/fixtures/user'

function buildForm(overrides: Partial<RegistrationForm> = {}): RegistrationForm {
  return {
    firstName: '  Ada ',
    lastName: ' Lovelace  ',
    email: ' ada@example.test ',
    phone: ' 600000000 ',
    password: ' spaced password ',
    confirmPassword: ' spaced password ',
    nationalId: ' x-1234567-l ',
    confirmNationalId: 'X1234567L',
    gender: 'Female',
    promotionalConsent: false,
    minors: [],
    ...overrides,
  }
}

describe('toRegisterRequest', () => {
  it('trims the contact fields, normalizes the DNI/NIE and drops both confirmations', () => {
    expect(toRegisterRequest(buildForm())).toEqual({
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phone: '600000000',
      password: ' spaced password ',
      nationalId: 'X1234567L',
      gender: 'Female',
      promotionalConsent: false,
      minors: [],
    })
  })

  it('maps every minor with trimmed names and without the client key', () => {
    const request = toRegisterRequest(
      buildForm({
        minors: [
          {
            key: 1,
            firstName: ' Byron ',
            lastName: ' King ',
            dateOfBirth: '2015-01-02',
            gender: 'Male',
          },
          {
            key: 2,
            firstName: 'Anne',
            lastName: 'King',
            dateOfBirth: '2017-03-04',
            gender: 'Other',
          },
        ],
      }),
    )

    expect(request.minors).toEqual([
      { firstName: 'Byron', lastName: 'King', birthDate: '2015-01-02', gender: 'Male' },
      { firstName: 'Anne', lastName: 'King', birthDate: '2017-03-04', gender: 'Other' },
    ])
  })

  it('throws when the adult has no gender', () => {
    expect(() => toRegisterRequest(buildForm({ gender: null }))).toThrow('missing gender')
  })

  it('throws when a minor has no gender', () => {
    const form = buildForm({
      minors: [{ key: 1, firstName: 'B', lastName: 'K', dateOfBirth: '2015-01-02', gender: null }],
    })

    expect(() => toRegisterRequest(form)).toThrow('missing minor gender')
  })
})

describe('toRegistrationResult', () => {
  it('keeps the adult id and number of minors', () => {
    expect(
      toRegistrationResult({
        adult: buildUserResponse({ id: 'adult-9' }),
        minors: [buildUserResponse({ id: 'm1' }), buildUserResponse({ id: 'm2' })],
      }),
    ).toEqual({ adultId: 'adult-9', minorCount: 2 })
  })

  it('defaults missing fields', () => {
    expect(toRegistrationResult({})).toEqual({ adultId: null, minorCount: 0 })
    expect(toRegistrationResult({ adult: {}, minors: null })).toEqual({
      adultId: null,
      minorCount: 0,
    })
  })
})
