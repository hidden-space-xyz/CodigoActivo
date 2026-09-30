import { describe, expect, it } from 'vitest'

import { toEventCategory, toEventCategoryRequest } from '@/entities/event-category/api/mapper'

import { buildEventCategoryType } from '../../../../support/builders'

describe('event category mapper', () => {
  it('maps a category', () => {
    expect(toEventCategory(buildEventCategoryType())).toEqual({
      id: 'category-1',
      name: 'Workshop',
      color: '#FF0000',
    })
  })

  it('sends the name and the color', () => {
    expect(toEventCategoryRequest({ name: 'Talk', color: '#00FF00' })).toEqual({
      name: 'Talk',
      color: '#00FF00',
    })
  })
})
