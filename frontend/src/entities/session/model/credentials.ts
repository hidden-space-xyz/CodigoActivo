/** Login form model; `identifier` is the account email, the only login identifier. */
export interface Credentials {
  identifier: string
  password: string
}

/** Blank login form state. */
export function createEmptyCredentials(): Credentials {
  return { identifier: '', password: '' }
}
