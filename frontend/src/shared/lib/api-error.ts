import { ApiError } from '@/shared/api/http-client'
import { i18n } from '@/shared/i18n'

export function getErrorMessage(error: unknown, fallback = i18n.global.t('errors.generic')): string {
  if (error instanceof ApiError) {
    const key = error.code ? `errors.${error.code}` : null
    if (key && i18n.global.te(key)) return i18n.global.t(key)
    return fallback
  }
  if (error instanceof Error && error.message) return error.message
  return fallback
}
