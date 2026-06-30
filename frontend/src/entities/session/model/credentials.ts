export interface Credentials {
  identifier: string
  password: string
}

export function createEmptyCredentials(): Credentials {
  return { identifier: '', password: '' }
}
