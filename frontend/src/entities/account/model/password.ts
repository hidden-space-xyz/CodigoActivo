/** Fewest characters the API accepts in a new password. */
const PASSWORD_MIN_LENGTH = 12

/** Whether a new password is shorter than the API accepts. */
export function isPasswordTooShort(password: string): boolean {
  return password.length < PASSWORD_MIN_LENGTH
}
