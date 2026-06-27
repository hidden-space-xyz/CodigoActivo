export interface AppEnv {
  readonly apiBaseUrl: string
  readonly isDev: boolean
}

export const env: AppEnv = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? '',
  isDev: import.meta.env.DEV,
}
