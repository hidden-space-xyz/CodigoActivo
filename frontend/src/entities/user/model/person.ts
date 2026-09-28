import type { Gender } from '@/shared/api/generated/models'
import { i18n, type TranslationKey } from '@/shared/i18n'
import { nationalIdError, normalizeNationalId, toDateOnly } from '@/shared/lib'

import type { UpdateUserInput } from './types'

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/
const ADULT_AGE = 18

/**
 * Editable fields of a person as a form binds them: plain text, the birth date as `YYYY-MM-DD` or
 * `''`, and no gender until one is chosen.
 */
export interface PersonDraft {
  firstName: string
  lastName: string
  gender: Gender | null
  email: string
  phone: string
  secondaryPhone: string
  nationalId: string
  promotionalConsent: boolean
  birthDate: string
}

/** Fields an independent account is filled in with; it has no birth date. */
export type IndependentDraft = Omit<PersonDraft, 'birthDate'>

/** Fields a dependent is filled in with; the guardian's contact details and DNI/NIE stand for it. */
export type DependentDraft = Pick<PersonDraft, 'firstName' | 'lastName' | 'gender' | 'birthDate'>

/** Field of a person that a rule can refuse. */
export type PersonField = Exclude<keyof PersonDraft, 'promotionalConsent'>

/**
 * Why a field is refused: missing, a malformed email, a secondary phone equal to the phone, a
 * DNI/NIE with the wrong shape or control letter, or a birth date in the future or of an adult.
 */
export type PersonProblem =
  | 'required'
  | 'emailFormat'
  | 'sameAsPhone'
  | 'nationalIdFormat'
  | 'nationalIdLetter'
  | 'futureDate'
  | 'notMinor'

/** Problems found in a draft by field; a field without an entry is valid. */
type PersonProblems = Partial<Record<PersonField, PersonProblem>>

/**
 * Independent account ready to send, normalized like the API stores it except for the email case:
 * trimmed text, a blank secondary phone as `null` and the DNI/NIE without spaces or hyphens.
 */
export interface IndependentPerson {
  readonly firstName: string
  readonly lastName: string
  readonly gender: Gender
  readonly email: string
  readonly phone: string
  readonly secondaryPhone: string | null
  readonly nationalId: string
  readonly promotionalConsent: boolean
}

/** Dependent ready to send, with trimmed names and a `YYYY-MM-DD` birth date. */
export interface DependentPerson {
  readonly firstName: string
  readonly lastName: string
  readonly gender: Gender
  readonly birthDate: string
}

/** Outcome of reading a draft: its problems and, only when there are none, the person to send. */
export interface ParsedPerson<T> {
  readonly problems: PersonProblems
  readonly person: T | null
}

/** Context a dependent's birth date is judged in. */
export interface DependentRules {
  /** Local day (`YYYY-MM-DD`) on which the dependent must be a minor; defaults to today. */
  readonly today?: string
  /**
   * Birth date already stored. Keeping it is always accepted, so a dependent who has come of age
   * stays editable; any other date must make it a minor.
   */
  readonly storedBirthDate?: string | null
}

/**
 * What a valid form submits: the person to send and, for an independent account whose change
 * needs it, the password of whoever makes the change.
 */
export type PersonSubmission =
  | {
      readonly kind: 'independent'
      readonly person: IndependentPerson
      readonly currentPassword: string | null
    }
  | { readonly kind: 'dependent'; readonly person: DependentPerson; readonly currentPassword: null }

const PROBLEM_MESSAGES: Record<PersonProblem, TranslationKey> = {
  required: 'entities.user.person.required',
  emailFormat: 'entities.user.person.emailFormat',
  sameAsPhone: 'entities.user.person.sameAsPhone',
  nationalIdFormat: 'validation.nationalIdFormat',
  nationalIdLetter: 'validation.nationalIdLetter',
  futureDate: 'entities.user.person.birthDateInvalid',
  notMinor: 'entities.user.person.birthDateNotMinor',
}

const REQUIRED_MESSAGES: Partial<Record<PersonField, TranslationKey>> = {
  gender: 'entities.user.person.genderRequired',
  nationalId: 'validation.nationalIdFormat',
  birthDate: 'entities.user.person.birthDateInvalid',
}

const NATIONAL_ID_PROBLEMS = { format: 'nationalIdFormat', letter: 'nationalIdLetter' } as const

function namesProblems(draft: Pick<PersonDraft, 'firstName' | 'lastName' | 'gender'>) {
  const problems: PersonProblems = {}
  if (!draft.firstName.trim()) problems.firstName = 'required'
  if (!draft.lastName.trim()) problems.lastName = 'required'
  if (!draft.gender) problems.gender = 'required'
  return problems
}

function nationalIdProblem(value: string): PersonProblem | undefined {
  if (!value.trim()) return 'required'
  const error = nationalIdError(value)
  return error ? NATIONAL_ID_PROBLEMS[error] : undefined
}

function ageOn(birthDate: string, today: string): number {
  const [birthYear = 0, birthMonth = 0, birthDay = 0] = birthDate.split('-').map(Number)
  const [year = 0, month = 0, day = 0] = today.split('-').map(Number)
  const beforeBirthday = month < birthMonth || (month === birthMonth && day < birthDay)
  return year - birthYear - (beforeBirthday ? 1 : 0)
}

function birthDateProblem(value: string | null, rules: DependentRules): PersonProblem | undefined {
  if (!value || !DATE_ONLY.test(value)) return 'required'
  if (value === rules.storedBirthDate) return undefined
  const today = rules.today ?? toDateOnly(new Date())
  if (value > today) return 'futureDate'
  return ageOn(value, today) < ADULT_AGE ? undefined : 'notMinor'
}

/**
 * Checks and normalizes an independent account: names, gender, a valid DNI/NIE, an email with an
 * address shape, a phone, and an optional secondary phone different from the phone.
 */
export function parseIndependentPerson(draft: IndependentDraft): ParsedPerson<IndependentPerson> {
  const problems = namesProblems(draft)
  const email = draft.email.trim()
  const phone = draft.phone.trim()
  const secondaryPhone = draft.secondaryPhone.trim()
  const nationalId = nationalIdProblem(draft.nationalId)
  if (nationalId) problems.nationalId = nationalId
  if (!email) problems.email = 'required'
  else if (!EMAIL_PATTERN.test(email)) problems.email = 'emailFormat'
  if (!phone) problems.phone = 'required'
  if (secondaryPhone && secondaryPhone === phone) problems.secondaryPhone = 'sameAsPhone'
  if (Object.keys(problems).length > 0 || !draft.gender) return { problems, person: null }
  return {
    problems,
    person: {
      firstName: draft.firstName.trim(),
      lastName: draft.lastName.trim(),
      gender: draft.gender,
      email,
      phone,
      secondaryPhone: secondaryPhone || null,
      nationalId: normalizeNationalId(draft.nationalId),
      promotionalConsent: draft.promotionalConsent,
    },
  }
}

/**
 * Checks and normalizes a dependent: names, gender and a birth date that is not in the future and
 * makes it a minor on `today`, unless it is the stored one.
 */
export function parseDependentPerson(
  draft: DependentDraft,
  rules: DependentRules = {},
): ParsedPerson<DependentPerson> {
  const problems = namesProblems(draft)
  const birthDate = birthDateProblem(draft.birthDate, rules)
  if (birthDate) problems.birthDate = birthDate
  if (Object.keys(problems).length > 0 || !draft.gender) return { problems, person: null }
  return {
    problems,
    person: {
      firstName: draft.firstName.trim(),
      lastName: draft.lastName.trim(),
      gender: draft.gender,
      birthDate: draft.birthDate,
    },
  }
}

/**
 * Birth dates a new dependent may have on `today`, as `YYYY-MM-DD` bounds for date inputs: from
 * the day after the date 18 years ago, which on 29 February is 1 March, up to today.
 */
export function minorBirthDateRange(today: string = toDateOnly(new Date())): {
  readonly min: string
  readonly max: string
} {
  const [year = 0, month = 1, day = 1] = today.split('-').map(Number)
  const lastDay = new Date(Date.UTC(year - ADULT_AGE, month, 0)).getUTCDate()
  const cutoff = new Date(Date.UTC(year - ADULT_AGE, month - 1, Math.min(day, lastDay) + 1))
  return { min: cutoff.toISOString().slice(0, 10), max: today }
}

/** Translated explanation of why `field` is refused, worded for the field when it is missing. */
export function personProblemMessage(field: PersonField, problem: PersonProblem): string {
  const key =
    problem === 'required'
      ? (REQUIRED_MESSAGES[field] ?? PROBLEM_MESSAGES.required)
      : PROBLEM_MESSAGES[problem]
  return i18n.global.t(key)
}

/**
 * Builds the admin update of a user from a submission: a dependent keeps `parentId` and sends
 * `null` contact details, DNI/NIE and no consent, while an independent account sends no birth date.
 */
export function toUpdateUserInput(
  submission: PersonSubmission,
  parentId: string | null,
): UpdateUserInput {
  if (submission.kind === 'dependent') {
    return {
      ...submission.person,
      email: null,
      phone: null,
      secondaryPhone: null,
      nationalId: null,
      promotionalConsent: false,
      parentId,
      currentPassword: null,
    }
  }
  return {
    ...submission.person,
    birthDate: null,
    parentId,
    currentPassword: submission.currentPassword,
  }
}
