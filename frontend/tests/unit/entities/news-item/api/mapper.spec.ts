import { describe, expect, it } from 'vitest'

import { toNewsItem, toNewsItemRequest, toNewsSummary } from '@/entities/news-item/api/mapper'

import {
  buildNewsItemResponse,
  buildNewsListItem,
  omit,
  richText,
} from '../../../../support/builders'

describe('toNewsSummary', () => {
  it('maps a list item keeping its publication instant', () => {
    expect(toNewsSummary(buildNewsListItem({ featured: true }))).toEqual({
      id: 'news-item-1',
      title: 'Abrimos inscripciones',
      subtitle: 'Nueva temporada',
      createdAt: '2026-02-01T10:00:00Z',
      thumbnailId: 'thumb-news-item',
      featured: true,
    })
  })
})

describe('toNewsItem', () => {
  it('adds the description and the last change to the summary', () => {
    expect(toNewsItem(buildNewsItemResponse())).toMatchObject({
      id: 'news-item-1',
      description: richText('Ya puedes apuntarte.'),
      updatedAt: '2026-02-03T10:00:00Z',
    })
  })

  it('reads a news item never edited as having no last change', () => {
    expect(toNewsItem(omit(buildNewsItemResponse(), 'updatedAt')).updatedAt).toBeNull()
  })
})

describe('toNewsItemRequest', () => {
  it('sends every field of the news item', () => {
    const input = {
      title: 'Demo day',
      subtitle: 'Friday',
      description: richText('Details'),
      thumbnailId: 'thumb',
    }

    expect(toNewsItemRequest(input)).toEqual(input)
  })
})
