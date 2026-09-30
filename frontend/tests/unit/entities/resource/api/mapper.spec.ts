import { describe, expect, it } from 'vitest'

import {
  toLearningResource,
  toLearningResourceSummary,
  toResourceRequest,
  toResourceType,
} from '@/entities/resource/api/mapper'

import {
  buildResourceListItem,
  buildResourceResponse,
  externalType,
  omit,
  richText,
} from '../../../../support/builders'

const ARTICLE = { id: 'type-article', name: 'Artículo', color: '#123456', isExternal: false }

describe('toResourceType', () => {
  it('keeps what classifies a resource', () => {
    expect(toResourceType(externalType)).toEqual({
      id: 'type-link',
      name: 'Enlace',
      color: '',
      isExternal: true,
    })
  })
})

describe('toLearningResourceSummary', () => {
  it('maps a list item keeping its creation instant', () => {
    expect(toLearningResourceSummary(buildResourceListItem())).toEqual({
      id: 'resource-1',
      title: 'Guía de Python',
      subtitle: 'Primeros pasos',
      type: ARTICLE,
      url: null,
      createdAt: '2026-03-02T10:00:00Z',
      thumbnailId: 'thumb-resource',
    })
  })

  it('reads a missing url as none', () => {
    expect(toLearningResourceSummary(omit(buildResourceListItem(), 'url')).url).toBeNull()
    expect(
      toLearningResourceSummary(buildResourceListItem({ url: 'https://scratch.mit.edu' })).url,
    ).toBe('https://scratch.mit.edu')
  })
})

describe('toLearningResource', () => {
  it('adds the description to the summary', () => {
    expect(toLearningResource(buildResourceResponse())).toMatchObject({
      id: 'resource-1',
      type: ARTICLE,
      description: richText('Todo sobre Python.'),
    })
  })
})

describe('toResourceRequest', () => {
  it('sends every field of the resource', () => {
    const input = {
      title: 'Vue',
      subtitle: 'Docs',
      description: null,
      url: 'https://vuejs.org',
      resourceTypeId: 'type-link',
      thumbnailId: 'thumb',
    }

    expect(toResourceRequest(input)).toEqual(input)
  })
})
