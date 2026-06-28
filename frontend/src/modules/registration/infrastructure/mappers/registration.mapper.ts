import type { RegistrationResult } from '@/modules/registration/domain/entities/registration-result.entity'
import type { RegistrationType } from '@/modules/registration/domain/entities/registration-type.entity'
import type { RegistrationForm } from '@/modules/registration/domain/value-objects/registration-form'
import type {
  RegisterRequest,
  RegisterResponse,
  UserTypeResponse,
} from '@/shared/api/generated/models'

export function toRegistrationType(type: UserTypeResponse): RegistrationType {
  return {
    id: type.id ?? '',
    name: type.name ?? '',
    description: type.description ?? '',
    color: type.color ?? '',
  }
}

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
    verificationCode: response.verificationCode ?? null,
    minorCount: response.minors?.length ?? 0,
  }
}
