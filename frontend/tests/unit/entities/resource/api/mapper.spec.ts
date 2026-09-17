import { describe, expect, it } from 'vitest'

import { toLearningResource, toLearningResourceSummary } from '@/entities/resource/api/mapper'
import { formatDate } from '@/shared/lib'

describe('resource mapper', () => {
  it('maps a list item with a formatted date and external url', () => {
    expect(
      toLearningResourceSummary({
        id: 'r1',
        title: 'Scratch',
        subtitle: 'Guía',
        createdAt: '2026-02-01T10:00:00Z',
        url: 'https://scratch.mit.edu',
        thumbnailId: 'thumb-1',
      }),
    ).toEqual({
      id: 'r1',
      title: 'Scratch',
      subtitle: 'Guía',
      date: formatDate('2026-02-01T10:00:00Z'),
      url: 'https://scratch.mit.edu',
      thumbnailId: 'thumb-1',
    })
  })

  it('defaults a bare list item and keeps a missing url as null', () => {
    expect(toLearningResourceSummary({ url: null })).toEqual({
      id: '',
      title: '',
      subtitle: '',
      date: '',
      url: null,
      thumbnailId: '',
    })
  })

  it('adds the description to the detail model', () => {
    expect(toLearningResource({ id: 'r1', description: '<p>Texto</p>' })).toMatchObject({
      id: 'r1',
      url: null,
      description: '<p>Texto</p>',
    })
    expect(toLearningResource({}).description).toBe('')
  })
})
