/**
 * Login form model; `identifier` is the account email, the only login identifier.
 * `keepSignedIn` carries the user's explicit request to stay signed in on this device.
 */
export interface Credentials {
  identifier: string
  password: string
  keepSignedIn: boolean
}

/** Blank login form state; the keep-signed-in box starts unticked. */
export function createEmptyCredentials(): Credentials {
  return { identifier: '', password: '', keepSignedIn: false }
}
