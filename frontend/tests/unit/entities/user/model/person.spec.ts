import { describe, expect, it } from 'vitest'

import {
  minorBirthDateRange,
  parseDependentPerson,
  parseIndependentPerson,
  personProblemMessage,
  toUpdateUserInput,
} from '@/entities/user'
import type { DependentDraft, IndependentDraft } from '@/entities/user/model/person'

import { t } from '../../../../support/render'

const TODAY = '2026-07-04'

function adult(overrides: Partial<IndependentDraft> = {}): IndependentDraft {
  return {
    firstName: ' Ada ',
    lastName: ' Lovelace ',
    gender: 'Female',
    email: ' ada@example.test ',
    phone: ' 600000000 ',
    secondaryPhone: '   ',
    nationalId: ' x-1234567-l ',
    promotionalConsent: true,
    ...overrides,
  }
}

function child(overrides: Partial<DependentDraft> = {}): DependentDraft {
  return {
    firstName: ' Byron ',
    lastName: ' King ',
    gender: 'Male',
    birthDate: '2016-07-04',
    ...overrides,
  }
}

describe('parseIndependentPerson', () => {
  it('normalizes a valid independent account', () => {
    expect(parseIndependentPerson(adult())).toEqual({
      problems: {},
      person: {
        firstName: 'Ada',
        lastName: 'Lovelace',
        gender: 'Female',
        email: 'ada@example.test',
        phone: '600000000',
        secondaryPhone: null,
        nationalId: 'X1234567L',
        promotionalConsent: true,
      },
    })
  })

  it.each([
    [{ firstName: '  ' }, { firstName: 'required' }],
    [{ lastName: '' }, { lastName: 'required' }],
    [{ gender: null }, { gender: 'required' }],
    [{ email: ' ' }, { email: 'required' }],
    [{ email: 'ada@example' }, { email: 'emailFormat' }],
    [{ phone: '' }, { phone: 'required' }],
    [{ secondaryPhone: ' 600000000' }, { secondaryPhone: 'sameAsPhone' }],
    [{ nationalId: ' ' }, { nationalId: 'required' }],
    [{ nationalId: 'X123' }, { nationalId: 'nationalIdFormat' }],
    [{ nationalId: 'X1234567A' }, { nationalId: 'nationalIdLetter' }],
  ] as const)('refuses %o with %o and no person', (overrides, problems) => {
    expect(parseIndependentPerson(adult(overrides))).toEqual({ problems, person: null })
  })
})

describe('parseDependentPerson', () => {
  it('trims the names and keeps the birth date', () => {
    expect(parseDependentPerson(child(), { today: TODAY })).toEqual({
      problems: {},
      person: { firstName: 'Byron', lastName: 'King', gender: 'Male', birthDate: '2016-07-04' },
    })
  })

  it.each([
    ['2008-07-05', null, undefined],
    ['2026-07-04', null, undefined],
    ['2008-07-04', null, 'notMinor'],
    ['2026-07-05', null, 'futureDate'],
    ['', null, 'required'],
    ['not-a-date', null, 'required'],
    ['2000-03-04', '2000-03-04', undefined],
    ['2000-03-05', '2000-03-04', 'notMinor'],
    ['2016-07-04', '2000-03-04', undefined],
  ] as const)(
    'judges the birth date %s, stored as %s, on the local day',
    (birthDate, storedBirthDate, problem) => {
      const parsed = parseDependentPerson(child({ birthDate }), { today: TODAY, storedBirthDate })

      expect(parsed.problems.birthDate).toBe(problem)
      expect(parsed.person === null).toBe(problem !== undefined)
    },
  )

  it('counts a birthday on 29 February as reached on 1 March in common years', () => {
    const leapling = child({ birthDate: '2008-02-29' })

    expect(parseDependentPerson(leapling, { today: '2026-02-28' }).problems).toEqual({})
    expect(parseDependentPerson(leapling, { today: '2026-03-01' }).problems).toEqual({
      birthDate: 'notMinor',
    })
  })

  it('requires names and a gender', () => {
    expect(
      parseDependentPerson(child({ firstName: '', lastName: ' ', gender: null }), {
        today: TODAY,
      }).problems,
    ).toEqual({ firstName: 'required', lastName: 'required', gender: 'required' })
  })
})

describe('minorBirthDateRange', () => {
  it.each([
    ['2026-07-04', '2008-07-05'],
    ['2024-02-29', '2006-03-01'],
    ['2026-12-31', '2009-01-01'],
  ])('on %s starts on %s and ends today', (today, min) => {
    expect(minorBirthDateRange(today)).toEqual({ min, max: today })
  })

  it('accepts exactly the dates that make a new dependent a minor', () => {
    const { min, max } = minorBirthDateRange(TODAY)

    expect(parseDependentPerson(child({ birthDate: min }), { today: TODAY }).person).not.toBeNull()
    expect(parseDependentPerson(child({ birthDate: max }), { today: TODAY }).person).not.toBeNull()
    expect(
      parseDependentPerson(child({ birthDate: '2008-07-04' }), { today: TODAY }).person,
    ).toBeNull()
  })
})

describe('personProblemMessage', () => {
  it.each([
    ['firstName', 'required', 'entities.user.person.required'],
    ['gender', 'required', 'entities.user.person.genderRequired'],
    ['nationalId', 'required', 'validation.nationalIdFormat'],
    ['birthDate', 'required', 'entities.user.person.birthDateInvalid'],
    ['email', 'emailFormat', 'entities.user.person.emailFormat'],
    ['secondaryPhone', 'sameAsPhone', 'entities.user.person.sameAsPhone'],
    ['nationalId', 'nationalIdLetter', 'validation.nationalIdLetter'],
    ['birthDate', 'futureDate', 'entities.user.person.birthDateInvalid'],
    ['birthDate', 'notMinor', 'entities.user.person.birthDateNotMinor'],
  ] as const)('explains %s %s', (field, problem, key) => {
    expect(personProblemMessage(field, problem)).toBe(t(key))
  })
})

describe('toUpdateUserInput', () => {
  it('sends an independent account without a birth date', () => {
    const person = parseIndependentPerson(adult()).person
    if (!person) throw new Error('invalid fixture')

    expect(
      toUpdateUserInput({ kind: 'independent', person, currentPassword: 'secret' }, null),
    ).toEqual({ ...person, birthDate: null, parentId: null, currentPassword: 'secret' })
  })

  it('sends a dependent with its guardian and nothing of its own to contact', () => {
    const person = parseDependentPerson(child(), { today: TODAY }).person
    if (!person) throw new Error('invalid fixture')

    expect(
      toUpdateUserInput({ kind: 'dependent', person, currentPassword: null }, 'guardian-1'),
    ).toEqual({
      ...person,
      email: null,
      phone: null,
      secondaryPhone: null,
      nationalId: null,
      promotionalConsent: false,
      parentId: 'guardian-1',
      currentPassword: null,
    })
  })
})
