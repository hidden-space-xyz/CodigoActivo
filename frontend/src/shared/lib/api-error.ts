import { ApiError } from '@/shared/api/http-client'

const GENERIC_ERROR_MESSAGE = 'Ha ocurrido un error. Inténtalo de nuevo.'

export function getErrorMessage(error: unknown, fallback = GENERIC_ERROR_MESSAGE): string {
  if (error instanceof ApiError) return GENERIC_ERROR_MESSAGE
  if (error instanceof Error && error.message) return error.message
  return fallback
}
