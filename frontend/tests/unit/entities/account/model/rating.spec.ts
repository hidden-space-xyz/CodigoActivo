import { describe, expect, it } from 'vitest'

import { isEventRatingEmpty } from '@/entities/account'

const EMPTY = { score: null, mostLiked: '', leastLiked: '', suggestions: '' }

describe('isEventRatingEmpty', () => {
  it('is empty without a score and with only blank answers', () => {
    expect(isEventRatingEmpty(EMPTY)).toBe(true)
    expect(isEventRatingEmpty({ ...EMPTY, mostLiked: '   ', suggestions: '\n' })).toBe(true)
  })

  it('is not empty with a score or any written answer', () => {
    expect(isEventRatingEmpty({ ...EMPTY, score: 1 })).toBe(false)
    expect(isEventRatingEmpty({ ...EMPTY, mostLiked: 'Todo' })).toBe(false)
    expect(isEventRatingEmpty({ ...EMPTY, leastLiked: 'Nada' })).toBe(false)
    expect(isEventRatingEmpty({ ...EMPTY, suggestions: 'Más' })).toBe(false)
  })
})
