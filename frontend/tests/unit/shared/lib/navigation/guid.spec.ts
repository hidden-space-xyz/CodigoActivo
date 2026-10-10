import { describe, expect, it } from 'vitest'
import { isGuid } from '@/shared/lib/navigation'

describe('isGuid', () => {
  it.each(['3f2504e0-4f89-41d3-9a0c-0305e82c3301', '3F2504E0-4F89-41D3-9A0C-0305E82C3301'])(
    'accepts %s',
    (value) => {
      expect(isGuid(value)).toBe(true)
    },
  )

  it.each([
    null,
    undefined,
    '',
    'user-1',
    '3f2504e04f8941d39a0c0305e82c3301',
    ' 3f2504e0-4f89-41d3-9a0c-0305e82c3301',
    '3f2504e0-4f89-41d3-9a0c-0305e82c3301/admin',
    '../users/3f2504e0-4f89-41d3-9a0c-0305e82c3301/admin?',
  ])('rejects %j', (value) => {
    expect(isGuid(value)).toBe(false)
  })
})
