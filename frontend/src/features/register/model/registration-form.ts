import type { Gender } from '@/shared/api/generated/models'

export interface MinorForm {
  key: number
  firstName: string
  lastName: string
  dateOfBirth: string
  gender: Gender | null
}

let minorKeySeq = 0

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

export function createEmptyMinor(): MinorForm {
  minorKeySeq += 1
  return { key: minorKeySeq, firstName: '', lastName: '', dateOfBirth: '', gender: null }
}

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
