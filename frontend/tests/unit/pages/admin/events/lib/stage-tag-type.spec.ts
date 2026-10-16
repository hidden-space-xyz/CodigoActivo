import { describe, expect, it } from 'vitest'

import { stageTagType } from '@/pages/admin/events/lib/stage-tag-type'

describe('stageTagType', () => {
  it.each([
    ['upcoming', 'info'],
    ['earlySignupOpen', 'warning'],
    ['signupOpen', 'success'],
    ['signupClosed', 'primary'],
    ['finished', 'primary'],
  ] as const)('shows the %s stage as a %s tag', (status, type) => {
    expect(stageTagType(status)).toBe(type)
  })
})
