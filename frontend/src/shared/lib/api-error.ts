import { ApiError } from '@/shared/api'
import { i18n } from '@/shared/i18n'

export function getErrorMessage(
  error: unknown,
  fallback = i18n.global.t('errors.generic'),
): string {
  if (error instanceof ApiError && error.code) {
    const key = `errors.${error.code}`
    if (i18n.global.te(key)) return i18n.global.t(key)
  }
  return fallback
}
