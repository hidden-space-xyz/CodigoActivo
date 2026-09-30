import { describe, expect, it } from 'vitest'
import { fileContentUrl } from '@/shared/api'

describe('fileContentUrl', () => {
  it('builds the same-origin content URL for a file id', () => {
    expect(fileContentUrl('abc-123')).toBe('/api/files/abc-123/content')
  })

  it('returns an empty string without an id', () => {
    expect(fileContentUrl(null)).toBe('')
    expect(fileContentUrl(undefined)).toBe('')
    expect(fileContentUrl('')).toBe('')
  })
})
