import { describe, expect, it } from 'vitest'
import { toLocalRedirect } from '@/shared/lib/navigation'

describe('toLocalRedirect', () => {
  it.each(['/', '/account', '/admin/users?search=ana&page=2', '/events#top'])(
    'accepts the local path %s',
    (value) => {
      expect(toLocalRedirect(value)).toBe(value)
    },
  )

  it.each([
    '//evil.test',
    '///evil.test',
    '/\\evil.test',
    '/account\\..\\admin',
    'https://evil.test',
    'account',
    '',
    'javascript:alert(1)',
    '/account\n/admin',
    '/account\u0000',
    '/account\u001f',
    '/account\u007f',
    `/account${String.fromCharCode(0x0b)}`,
  ])('rejects %j', (value) => {
    expect(toLocalRedirect(value)).toBeNull()
  })

  it.each([undefined, null, 42, ['/account', '/admin'], { path: '/account' }])(
    'rejects the non-string value %j',
    (value) => {
      expect(toLocalRedirect(value)).toBeNull()
    },
  )
})
