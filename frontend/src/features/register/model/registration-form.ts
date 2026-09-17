import type { Gender } from '@/shared/api/generated/models'

/** Editable data for a minor registered alongside the adult. */
export interface MinorForm {
  /** Client-only id, unique per page load, used as the `v-for` key. */
  key: number
  firstName: string
  lastName: string
  dateOfBirth: string
  gender: Gender | null
}

let minorKeySeq = 0

/** Editable state of the registration form; every `dateOfBirth` holds a `YYYY-MM-DD` string. */
export interface RegistrationForm {
  firstName: string
  lastName: string
  email: string
  phone: string
  password: string
  confirmPassword: string
  dateOfBirth: string
  gender: Gender | null
  minors: MinorForm[]
}

/** Blank minor row with a fresh `key`. */
export function createEmptyMinor(): MinorForm {
  minorKeySeq += 1
  return { key: minorKeySeq, firstName: '', lastName: '', dateOfBirth: '', gender: null }
}

/** Blank registration form with no minors, used initially and when the flow is reset. */
export function createEmptyRegistrationForm(): RegistrationForm {
  return {
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    password: '',
    confirmPassword: '',
    dateOfBirth: '',
    gender: null,
    minors: [],
  }
}
