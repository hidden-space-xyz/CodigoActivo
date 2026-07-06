import type { RegisterRequest, RegisterResponse } from '@/shared/api/generated/models'

import type { RegistrationForm } from '../model/registration-form'
import type { RegistrationResult } from '../model/types'

export function toRegisterRequest(form: RegistrationForm): RegisterRequest {
  return {
    firstName: form.firstName.trim(),
    lastName: form.lastName.trim(),
    email: form.email.trim(),
    phone: form.phone.trim(),
    password: form.password,
    birthDate: form.dateOfBirth,
    roleId: form.roleId,
    minors: form.minors.map((minor) => ({
      firstName: minor.firstName.trim(),
      lastName: minor.lastName.trim(),
      birthDate: minor.dateOfBirth,
      roleId: minor.roleId,
    })),
  }
}

export function toRegistrationResult(response: RegisterResponse): RegistrationResult {
  return {
    adultId: response.adult?.id ?? null,
    requiresVerification: response.requiresVerification ?? false,
    minorCount: response.minors?.length ?? 0,
  }
}
