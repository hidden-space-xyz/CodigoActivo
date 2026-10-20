import { PASSWORD_MIN_LENGTH } from '@/shared/config'

/** Whether a new password is shorter than the API accepts. */
export function isPasswordTooShort(password: string): boolean {
  return password.length < PASSWORD_MIN_LENGTH
}
