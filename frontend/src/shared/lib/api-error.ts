import { ApiError } from '@/shared/api/http-client'
import { errorMessages } from '@/shared/api/error-messages'

const GENERIC_ERROR_MESSAGE = 'Ha ocurrido un error. Inténtalo de nuevo.'

export function getErrorMessage(error: unknown, fallback = GENERIC_ERROR_MESSAGE): string {
  if (error instanceof ApiError) {
    return (error.code && errorMessages[error.code]) || GENERIC_ERROR_MESSAGE
  }
  if (error instanceof Error && error.message) return error.message
  return fallback
}
