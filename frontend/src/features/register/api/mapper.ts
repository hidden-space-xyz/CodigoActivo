import type {
  RegisterMinorRequest,
  RegisterRequest,
  RegisterResponse,
} from '@/shared/api/generated/models'
import { normalizeNationalId } from '@/shared/lib'

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

/**
 * Builds the register request from the form, trimming names, email and phones, sending a blank
 * secondary phone as `null`, normalizing the DNI or NIE and dropping the password confirmation.
 * Throws if the adult or any minor has no gender; the form validates this first.
 */
export function toRegisterRequest(form: RegistrationForm): RegisterRequest {
  const { gender } = form
  if (!gender) throw new Error('missing gender')
  return {
    firstName: form.firstName.trim(),
    lastName: form.lastName.trim(),
    email: form.email.trim(),
    phone: form.phone.trim(),
    secondaryPhone: form.secondaryPhone.trim() || null,
    password: form.password,
    nationalId: normalizeNationalId(form.nationalId),
    gender,
    promotionalConsent: form.promotionalConsent,
    minors: form.minors.map(toRegisterMinorRequest),
  }
}

/** Reduces the register response to what the success step needs, defaulting missing fields. */
export function toRegistrationResult(response: RegisterResponse): RegistrationResult {
  return {
    adultId: response.adult?.id ?? null,
    minorCount: response.minors?.length ?? 0,
  }
}
