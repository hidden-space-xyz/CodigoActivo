import { describe, expect, it } from 'vitest'

import { ErrorCode } from '@/shared/api/generated/models'
import es from '@/shared/i18n/locales/es.json'

describe('error messages', () => {
  it('exist only for the generic fallback and the ErrorCode values of the API contract', () => {
    const codes = new Set<string>(Object.values(ErrorCode))
    const messages = Object.keys(es.errors)
    expect(messages).toContain('generic')

    expect(
      messages.filter((key) => key !== 'generic' && !codes.has(key)),
      'Remove the errors.* messages of codes that swagger.json no longer declares',
    ).toEqual([])
  })
})
