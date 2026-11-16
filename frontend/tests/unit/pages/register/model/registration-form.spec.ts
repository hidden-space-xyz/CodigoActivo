import { describe, expect, it } from 'vitest'

import {
  canAddMinor,
  createEmptyMinor,
  createEmptyRegistrationForm,
} from '@/pages/register/model/registration-form'
import { MAX_DEPENDENTS } from '@/shared/config'

describe('registration form factories', () => {
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
      birthDate: '',
      gender: null,
    })
    expect(second.key).toBeGreaterThan(first.key)
  })

  it('allows minors up to the most one guardian may hold', () => {
    const form = createEmptyRegistrationForm()
    form.minors = Array.from({ length: MAX_DEPENDENTS - 1 }, createEmptyMinor)

    expect(canAddMinor(form)).toBe(true)
    form.minors.push(createEmptyMinor())
    expect(canAddMinor(form)).toBe(false)
  })
})
