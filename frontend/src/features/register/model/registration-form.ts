export interface MinorForm {
  firstName: string
  lastName: string
  dateOfBirth: string
  roleId: string
}

export interface RegistrationForm {
  firstName: string
  lastName: string
  email: string
  phone: string
  password: string
  dateOfBirth: string
  roleId: string
  minors: MinorForm[]
}

export function createEmptyMinor(): MinorForm {
  return { firstName: '', lastName: '', dateOfBirth: '', roleId: '' }
}

export function createEmptyRegistrationForm(): RegistrationForm {
  return {
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    password: '',
    dateOfBirth: '',
    roleId: '',
    minors: [],
  }
}
