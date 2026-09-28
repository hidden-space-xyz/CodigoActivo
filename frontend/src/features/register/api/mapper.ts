import { parseDependentPerson, parseIndependentPerson } from '@/entities/user'
import type { RegisterRequest, RegisterResponse } from '@/shared/api/generated/models'

import type { RegistrationForm } from '../model/registration-form'
import type { RegistrationResult } from '../model/types'

/**
 * Builds the register request from the form with the person rules of `@/entities/user`: trimmed
 * names, email and phones, a blank secondary phone as `null` and a normalized DNI or NIE. The
 * password confirmation is dropped. Throws if the adult or any minor breaks a rule; the form
 * validates them first.
 */
export function toRegisterRequest(form: RegistrationForm): RegisterRequest {
  const adult = parseIndependentPerson(form).person
  if (!adult) throw new Error('invalid registrant')
  const minors = form.minors.map((minor) => {
    const person = parseDependentPerson(minor).person
    if (!person) throw new Error('invalid minor')
    return person
  })
  return { ...adult, password: form.password, minors }
}

/** Reduces the register response to what the success step needs, defaulting missing fields. */
export function toRegistrationResult(response: RegisterResponse): RegistrationResult {
  return {
    adultId: response.adult?.id ?? null,
    minorCount: response.minors?.length ?? 0,
  }
}
