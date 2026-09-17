/** Login form model; `identifier` is the user email or phone number. */
export interface Credentials {
  identifier: string
  password: string
}

/** Blank login form state. */
export function createEmptyCredentials(): Credentials {
  return { identifier: '', password: '' }
}
