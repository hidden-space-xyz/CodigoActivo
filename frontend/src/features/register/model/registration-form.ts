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

/**
 * Editable state of the registration form. Only minors carry a `dateOfBirth` (`YYYY-MM-DD`); the
 * adult is identified by a DNI or NIE typed twice (`nationalId` and `confirmNationalId`).
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
  confirmNationalId: string
  gender: Gender | null
  /** Optional agreement to receive promotional content; unchecked by default. */
  promotionalConsent: boolean
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
    secondaryPhone: '',
    password: '',
    confirmPassword: '',
    nationalId: '',
    confirmNationalId: '',
    gender: null,
    promotionalConsent: false,
    minors: [],
  }
}
