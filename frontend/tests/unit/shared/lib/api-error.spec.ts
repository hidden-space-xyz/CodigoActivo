import { describe, expect, it } from 'vitest'

import { ApiError } from '@/shared/api'
import { ErrorCode } from '@/shared/api/generated/models'
import { getErrorMessage } from '@/shared/lib'

import { t } from '../../../support/render'

describe('getErrorMessage', () => {
  it('returns the localized message for a known API error code', () => {
    const error = new ApiError(404, 'Server text', 'trace', ErrorCode.EventNotFound)

    expect(getErrorMessage(error)).toBe(t('errors.EventNotFound'))
  })

  it('falls back to the generic message for API errors without a code', () => {
    expect(getErrorMessage(new ApiError(500, 'Raw server text'))).toBe(t('errors.generic'))
  })

  it('falls back when the code has no translation', () => {
    const error = new ApiError(400, 'Raw', undefined, 'UnknownCode' as ErrorCode)

    expect(getErrorMessage(error, 'Custom fallback')).toBe('Custom fallback')
  })

  it('describes the guardianship errors without calling the dependent a minor', () => {
    const reassignment = getErrorMessage(
      new ApiError(403, 'Raw', undefined, ErrorCode.UserParentReassignmentForbidden),
    )
    const standalone = getErrorMessage(
      new ApiError(400, 'Raw', undefined, ErrorCode.UserParentNotAllowedForAdult),
    )

    expect(reassignment).toContain('persona a cargo')
    expect(reassignment).not.toMatch(/menor|mayor de edad/i)
    expect(standalone).toContain('cuenta propia')
    expect(standalone).not.toMatch(/menor|mayor de edad/i)
  })

  it('never exposes the text of other errors', () => {
    expect(getErrorMessage(new Error('Secret stack'))).toBe(t('errors.generic'))
    expect(getErrorMessage('string failure', 'Fallback')).toBe('Fallback')
  })
})
