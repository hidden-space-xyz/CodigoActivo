import type { RegistrationRepository } from '@/modules/registration/domain/repositories/registration-repository'

export function verifyRegistration(
  repository: RegistrationRepository,
  userId: string,
  otp: string,
): Promise<void> {
  return repository.verify(userId, otp)
}
