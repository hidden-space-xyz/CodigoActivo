import { describe, expect, it } from 'vitest'
import { displayUrl, toWebUrl } from '@/shared/lib/navigation'

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

describe('displayUrl', () => {
  it.each([
    ['https://example.test', 'example.test'],
    ['http://www.example.test/', 'example.test'],
    ['https://WWW.Example.test/docs/', 'example.test/docs'],
    ['https://example.test/docs/guide?lang=es#intro', 'example.test/docs/guide?lang=es#intro'],
    ['https://example.test:8443/a/', 'example.test:8443/a'],
    ['https://example.test/a%C3%B1o', 'example.test/año'],
    ['https://example.test/100%', 'example.test/100%'],
    ['https://www.españa.test/año/', 'españa.test/año'],
    ['https://app.test/#/docs/', 'app.test/#/docs/'],
    ['https://example.test/?next=/', 'example.test/?next=/'],
    ['https://example.test/docs/?x=1', 'example.test/docs/?x=1'],
    ['https://example.test/a%E2%80%AEb', 'example.test/ab'],
  ])('shows %j as %j', (value, expected) => {
    expect(displayUrl(value)).toBe(expected)
  })

  it('returns an address that does not parse unchanged', () => {
    expect(displayUrl('example.test/docs')).toBe('example.test/docs')
  })
})
