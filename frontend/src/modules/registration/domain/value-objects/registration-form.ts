import type { RegistrationRole } from '@/modules/registration/domain/value-objects/registration-role'

export interface GuardianDetails {
  firstName: string
  lastName: string
  email: string
  phone: string
}

export interface RegistrationForm {
  role: RegistrationRole
  firstName: string
  lastName: string
  email: string
  phone: string
  password: string
  dateOfBirth: string
  guardian: GuardianDetails
}

export function createEmptyGuardian(): GuardianDetails {
  return { firstName: '', lastName: '', email: '', phone: '' }
}

export function createEmptyRegistrationForm(role: RegistrationRole): RegistrationForm {
  return {
    role,
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    password: '',
    dateOfBirth: '',
    guardian: createEmptyGuardian(),
  }
}
