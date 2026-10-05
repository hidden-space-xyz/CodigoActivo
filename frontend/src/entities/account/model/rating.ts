import type { EventRatingInput } from './account-inputs'

/** Whether a rating has neither a score nor any written answer, which the API rejects. */
export function isEventRatingEmpty(input: EventRatingInput): boolean {
  return (
    input.score === null &&
    [input.mostLiked, input.leastLiked, input.suggestions].every((answer) => answer.trim() === '')
  )
}
