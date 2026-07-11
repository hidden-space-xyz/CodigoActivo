interface MinorForm {
  key: number
  firstName: string
  lastName: string
  dateOfBirth: string
}

let minorKeySeq = 0

export interface RegistrationForm {
  firstName: string
  lastName: string
  email: string
  phone: string
  password: string
  dateOfBirth: string
  minors: MinorForm[]
}

export function createEmptyMinor(): MinorForm {
  minorKeySeq += 1
  return { key: minorKeySeq, firstName: '', lastName: '', dateOfBirth: '' }
}

export function createEmptyRegistrationForm(): RegistrationForm {
  return {
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    password: '',
    dateOfBirth: '',
    minors: [],
  }
}
