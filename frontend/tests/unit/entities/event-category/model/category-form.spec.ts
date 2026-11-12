import { describe, expect, it } from 'vitest'

import {
  readEventCategoryDraft,
  toEventCategoryDraft,
} from '@/entities/event-category/model/category-form'
import { DEFAULT_TAG_COLOR } from '@/shared/lib/color'

describe('toEventCategoryDraft', () => {
  it('starts with the default color and fills an edited category', () => {
    expect(toEventCategoryDraft(null)).toEqual({ name: '', color: DEFAULT_TAG_COLOR })
    expect(toEventCategoryDraft({ id: 'c1', name: 'Workshop', color: '#FF0000' })).toEqual({
      name: 'Workshop',
      color: '#FF0000',
    })
  })
})

describe('readEventCategoryDraft', () => {
  it('refuses a blank name without a message', () => {
    expect(readEventCategoryDraft({ name: '  ', color: '#FF0000' })).toEqual({
      problems: { name: true },
      value: null,
    })
  })

  it('trims the name and sends the color as hex', () => {
    expect(readEventCategoryDraft({ name: ' Talk ', color: '00FF00' })).toEqual({
      problems: {},
      value: { name: 'Talk', color: '#00FF00' },
    })
  })
})
