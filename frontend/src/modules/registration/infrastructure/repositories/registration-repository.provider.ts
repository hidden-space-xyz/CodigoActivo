import type { RegistrationRepository } from '@/modules/registration/domain/repositories/registration-repository'

import { HttpRegistrationRepository } from './http-registration.repository'

export const registrationRepository: RegistrationRepository = new HttpRegistrationRepository()
