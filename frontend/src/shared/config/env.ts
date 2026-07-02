interface AppEnv {
  readonly apiBaseUrl: string
}

export const env: AppEnv = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? '',
}
