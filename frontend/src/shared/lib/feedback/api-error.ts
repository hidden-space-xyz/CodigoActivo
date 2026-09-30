import { ApiError } from '@/shared/api'
import { i18n, type TranslationKey } from '@/shared/i18n'

/**
 * Localized message for an `ApiError` code from `errors.<code>`. Any other error, or a code without
 * a translation, yields `fallback` so raw server text never reaches the user.
 */
export function getErrorMessage(
  error: unknown,
  fallback = i18n.global.t('errors.generic'),
): string {
  if (error instanceof ApiError && error.code) {
    const key: TranslationKey = `errors.${error.code}`
    if (i18n.global.te(key)) return i18n.global.t(key)
  }
  return fallback
}
