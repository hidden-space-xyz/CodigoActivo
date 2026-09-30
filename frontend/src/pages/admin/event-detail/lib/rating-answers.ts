import type { TranslationKey } from '@/shared/i18n'

import type { EventRating } from '../model/types'

/** An answered question of a rating: which question and what the attendee wrote. */
export interface RatingAnswer {
  readonly labelKey: TranslationKey
  readonly value: string
}

/** The questions of a rating that were answered, in the order the rating form asks them. */
export function ratingAnswers(rating: EventRating): RatingAnswer[] {
  const answers: RatingAnswer[] = [
    { labelKey: 'entities.event.ratingQuestions.mostLiked', value: rating.mostLiked },
    { labelKey: 'entities.event.ratingQuestions.leastLiked', value: rating.leastLiked },
    { labelKey: 'entities.event.ratingQuestions.suggestions', value: rating.suggestions },
  ]
  return answers.filter((answer) => answer.value.trim() !== '')
}
