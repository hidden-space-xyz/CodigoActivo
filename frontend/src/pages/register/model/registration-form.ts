import type { RegistrationInput } from '@/entities/account'
import { parseDependentPerson, parseIndependentPerson, type Gender } from '@/entities/user'

/** Editable data for a minor registered alongside the adult. */
export interface MinorForm {
  /** Client-only id, unique per page load, used as the `v-for` key. */
  key: number
  firstName: string
  lastName: string
  birthDate: string
  gender: Gender | null
}

let minorKeySeq = 0

/**
 * Editable state of the registration form. Only minors carry a `birthDate` (`YYYY-MM-DD`); the
 * adult is identified by a DNI or NIE, typed once because its control letter already catches typos.
 */
export interface RegistrationForm {
  firstName: string
  lastName: string
  email: string
  phone: string
  /** Optional second contact phone; blank means none. */
  secondaryPhone: string
  password: string
  confirmPassword: string
  nationalId: string
  gender: Gender | null
  /** Optional agreement to receive promotional content; unchecked by default. */
  promotionalConsent: boolean
  minors: MinorForm[]
}

/** Blank minor row with a fresh `key`. */
export function createEmptyMinor(): MinorForm {
  minorKeySeq += 1
  return { key: minorKeySeq, firstName: '', lastName: '', birthDate: '', gender: null }
}

/** Blank registration form with no minors, used initially and when the flow is reset. */
export function createEmptyRegistrationForm(): RegistrationForm {
  return {
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    secondaryPhone: '',
    password: '',
    confirmPassword: '',
    nationalId: '',
    gender: null,
    promotionalConsent: false,
    minors: [],
  }
}

/**
 * Reads the form into a registration with the person rules of `@/entities/user`: trimmed names,
 * email and phones, a blank secondary phone as `null` and a normalized DNI or NIE; the password
 * confirmation is dropped. `null` while the adult or any minor breaks a rule, which the form shows
 * before submitting.
 */
export function toRegistrationInput(form: RegistrationForm): RegistrationInput | null {
  const adult = parseIndependentPerson(form).person
  const minors = form.minors.map((minor) => parseDependentPerson(minor).person)
  if (!adult || minors.some((minor) => minor === null)) return null
  return {
    adult,
    password: form.password,
    minors: minors.filter((minor) => minor !== null),
  }
}
