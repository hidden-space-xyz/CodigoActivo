import type {
  RegisterMinorRequest,
  RegisterRequest,
  RegisterResponse,
} from '@/shared/api/generated/models'

import type { MinorForm, RegistrationForm } from '../model/registration-form'
import type { RegistrationResult } from '../model/types'

function toRegisterMinorRequest(minor: MinorForm): RegisterMinorRequest {
  const { gender } = minor
  if (!gender) throw new Error('missing minor gender')
  return {
    firstName: minor.firstName.trim(),
    lastName: minor.lastName.trim(),
    birthDate: minor.dateOfBirth,
    gender,
  }
}

export function toRegisterRequest(form: RegistrationForm): RegisterRequest {
  const { gender } = form
  if (!gender) throw new Error('missing gender')
  return {
    firstName: form.firstName.trim(),
    lastName: form.lastName.trim(),
    email: form.email.trim(),
    phone: form.phone.trim(),
    password: form.password,
    birthDate: form.dateOfBirth,
    gender,
    minors: form.minors.map(toRegisterMinorRequest),
  }
}

export function toRegistrationResult(response: RegisterResponse): RegistrationResult {
  return {
    adultId: response.adult?.id ?? null,
    requiresVerification: response.requiresVerification ?? false,
    minorCount: response.minors?.length ?? 0,
  }
}
