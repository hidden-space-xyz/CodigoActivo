import { describe, expect, it } from 'vitest'
import { hasControlCharacter, toLocalRedirect } from '@/shared/lib/navigation'

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

describe('hasControlCharacter', () => {
  it.each(['/account', 'https://example.org/a b', 'mailto:ana@example.org', ''])(
    'finds none in %j',
    (value) => {
      expect(hasControlCharacter(value)).toBe(false)
    },
  )

  it.each(['/\t/evil.test', '/\n\\evil.test', 'java\rscript:', '/a\u0000', '/a\u007f'])(
    'finds one in %j',
    (value) => {
      expect(hasControlCharacter(value)).toBe(true)
    },
  )
})
