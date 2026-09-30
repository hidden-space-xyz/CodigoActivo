import { describe, expect, it } from 'vitest'

import {
  DEFAULT_CATEGORY_COLOR,
  readEventCategoryDraft,
  toEventCategoryDraft,
  toHexColor,
} from '@/entities/event-category/model/category-form'

describe('toHexColor', () => {
  it('writes a color with a single leading hash', () => {
    expect(toHexColor('00FF00')).toBe('#00FF00')
    expect(toHexColor('#00FF00')).toBe('#00FF00')
  })
})

describe('toEventCategoryDraft', () => {
  it('starts with the default color and fills an edited category', () => {
    expect(toEventCategoryDraft(null)).toEqual({ name: '', color: DEFAULT_CATEGORY_COLOR })
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
