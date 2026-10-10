import { describe, expect, it } from 'vitest'
import { toWebUrl } from '@/shared/lib/navigation'

describe('toWebUrl', () => {
  it.each([
    ['https://example.test', 'https://example.test'],
    [' http://example.test/path?q=1#top ', 'http://example.test/path?q=1#top'],
    ['HTTPS://Example.test', 'HTTPS://Example.test'],
  ])('accepts %j as %j', (value, expected) => {
    expect(toWebUrl(value)).toBe(expected)
  })

  it.each([
    null,
    undefined,
    '',
    '   ',
    'example.test',
    '/events',
    'javascript:alert(1)',
    'data:text/html,hi',
    'mailto:ana@example.test',
    'ftp://example.test',
    'https://',
    'https://user:secret@example.test',
    'https://user@example.test',
    'https://exa\tmple.test',
    'https://example.test/a\nb',
  ])('refuses %j', (value) => {
    expect(toWebUrl(value)).toBeNull()
  })
})
