import { ApiError } from '@/shared/api/http-client'

export function getErrorMessage(
  error: unknown,
  fallback = 'Ha ocurrido un error. Inténtalo de nuevo.',
): string {
  if (error instanceof ApiError) return error.message || fallback
  if (error instanceof Error && error.message) return error.message
  return fallback
}
