import { describe, expect, it } from 'vitest'

import { ratingAnswers } from '@/pages/admin/event-detail/lib/rating-answers'

describe('ratingAnswers', () => {
  it('lists the answered questions in the order the rating form asks them', () => {
    expect(
      ratingAnswers({
        id: 'rating-1',
        score: 4,
        mostLiked: 'The people',
        leastLiked: '  ',
        suggestions: 'More robots',
      }),
    ).toEqual([
      { labelKey: 'entities.event.ratingQuestions.mostLiked', value: 'The people' },
      { labelKey: 'entities.event.ratingQuestions.suggestions', value: 'More robots' },
    ])
  })

  it('has no answers for a score alone', () => {
    expect(
      ratingAnswers({ id: 'rating-1', score: 5, mostLiked: '', leastLiked: '', suggestions: '' }),
    ).toEqual([])
  })
})
