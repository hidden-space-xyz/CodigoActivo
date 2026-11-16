import { describe, expect, it } from 'vitest'
import { fileContentUrl, fileThumbnailUrl } from '@/shared/api'

describe.each([
  ['fileContentUrl', fileContentUrl, 'content'],
  ['fileThumbnailUrl', fileThumbnailUrl, 'thumbnail'],
])('%s', (_name, url, kind) => {
  it('builds the same-origin URL for a file id', () => {
    expect(url('abc-123')).toBe(`/api/files/abc-123/${kind}`)
  })

  it('returns an empty string without an id', () => {
    expect(url(null)).toBe('')
    expect(url(undefined)).toBe('')
    expect(url('')).toBe('')
  })
})
