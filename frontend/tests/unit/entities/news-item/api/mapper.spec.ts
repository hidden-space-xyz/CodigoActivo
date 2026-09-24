import { describe, expect, it } from 'vitest'

import { toNewsItem, toNewsSummary } from '@/entities/news-item/api/mapper'
import { formatDate } from '@/shared/lib'

describe('news mapper', () => {
  it('maps a list item formatting the creation date for display', () => {
    const summary = toNewsSummary({
      id: 'news-item-1',
      title: 'Nueva edición',
      subtitle: 'Inscripciones abiertas',
      createdAt: '2026-03-15T10:00:00Z',
      thumbnailId: 'thumb-1',
      featured: true,
    })

    expect(summary).toEqual({
      id: 'news-item-1',
      title: 'Nueva edición',
      subtitle: 'Inscripciones abiertas',
      date: formatDate('2026-03-15T10:00:00Z'),
      thumbnailId: 'thumb-1',
      featured: true,
    })
    expect(summary.date).toMatch(/2026/)
  })

  it('defaults a bare list item to empty values and no date', () => {
    expect(toNewsSummary({})).toEqual({
      id: '',
      title: '',
      subtitle: '',
      date: '',
      thumbnailId: '',
      featured: false,
    })
  })

  it('maps the full news item keeping the raw timestamps', () => {
    expect(
      toNewsItem({
        id: 'news-item-1',
        title: 'Nueva edición',
        description: '<p>Texto</p>',
        createdAt: '2026-03-15T10:00:00Z',
        updatedAt: '2026-03-16T10:00:00Z',
      }),
    ).toMatchObject({
      id: 'news-item-1',
      description: '<p>Texto</p>',
      publishedAt: '2026-03-15T10:00:00Z',
      updatedAt: '2026-03-16T10:00:00Z',
    })
    expect(toNewsItem({})).toMatchObject({
      description: '',
      publishedAt: null,
      updatedAt: null,
    })
  })
})
