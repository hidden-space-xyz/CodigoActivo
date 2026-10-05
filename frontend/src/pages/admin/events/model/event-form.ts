import type { EventDetail, EventInput } from '@/entities/event'
import { isDayAfter, parseDateOnly, toDateOnly } from '@/shared/lib/date'
import type { FormProblem, FormReading } from '@/shared/lib/form'
import { EMPTY_DOC_JSON } from '@/shared/lib/rich-text'

/** What the event dialog binds its inputs to; each date picker holds `null` until one is picked. */
export interface EventDraft {
  title: string
  subtitle: string
  /** Rich-text document. */
  description: string
  categoryIds: string[]
  /** Linked terms documents in the order the event shows them. */
  termsDocumentIds: string[]
  /** Whether deciding on each linked document is required, by document id; `true` when unset. */
  termsRequired: Record<string, boolean>
  startsAt: Date | null
  endsAt: Date | null
  earlySignupStartsAt: Date | null
  signupStartsAt: Date | null
  signupEndsAt: Date | null
}

/** Field of the event form that can be refused. */
type EventField =
  | 'title'
  | 'subtitle'
  | 'categoryIds'
  | 'startsAt'
  | 'endsAt'
  | 'earlySignupStartsAt'
  | 'signupStartsAt'
  | 'signupEndsAt'

/** A blank draft, or one filled with the event being edited. */
export function toEventDraft(event: EventDetail | null): EventDraft {
  const terms = [...(event?.terms ?? [])].sort((a, b) => a.displayOrder - b.displayOrder)
  return {
    title: event?.title ?? '',
    subtitle: event?.subtitle ?? '',
    description: event?.description ?? '',
    categoryIds: event?.categories.map((category) => category.id) ?? [],
    termsDocumentIds: terms.map((document) => document.id),
    termsRequired: Object.fromEntries(terms.map((document) => [document.id, document.required])),
    startsAt: parseDateOnly(event?.startsAt),
    endsAt: parseDateOnly(event?.endsAt),
    earlySignupStartsAt: event?.earlySignupStartsAt ? new Date(event.earlySignupStartsAt) : null,
    signupStartsAt: event ? new Date(event.signupStartsAt) : null,
    signupEndsAt: event ? new Date(event.signupEndsAt) : null,
  }
}

function readProblems(draft: EventDraft): Partial<Record<EventField, FormProblem>> {
  const { startsAt, endsAt, earlySignupStartsAt, signupStartsAt, signupEndsAt } = draft
  const problems: Partial<Record<EventField, FormProblem>> = {}
  if (!draft.title.trim()) problems.title = true
  if (!draft.subtitle.trim()) problems.subtitle = true
  if (draft.categoryIds.length === 0) {
    problems.categoryIds = 'pages.admin.events.form.problems.categoriesRequired'
  }
  if (!startsAt) problems.startsAt = 'pages.admin.events.form.problems.eventStartRequired'
  if (!endsAt) problems.endsAt = 'pages.admin.events.form.problems.eventEndRequired'
  else if (startsAt && endsAt < startsAt) {
    problems.endsAt = 'pages.admin.events.form.problems.eventOrderInvalid'
  }
  if (!signupStartsAt) {
    problems.signupStartsAt = 'pages.admin.events.form.problems.signupStartRequired'
  } else if (isDayAfter(signupStartsAt, endsAt)) {
    problems.signupStartsAt = 'pages.admin.events.form.problems.signupAfterEventEnd'
  }
  if (!signupEndsAt) {
    problems.signupEndsAt = 'pages.admin.events.form.problems.signupEndRequired'
  } else if (signupStartsAt && signupEndsAt <= signupStartsAt) {
    problems.signupEndsAt = 'pages.admin.events.form.problems.signupOrderInvalid'
  } else if (isDayAfter(signupEndsAt, endsAt)) {
    problems.signupEndsAt = 'pages.admin.events.form.problems.signupEndAfterEventEnd'
  }
  if (earlySignupStartsAt && signupStartsAt && earlySignupStartsAt >= signupStartsAt) {
    problems.earlySignupStartsAt = 'pages.admin.events.form.problems.earlySignupOrderInvalid'
  }
  return problems
}

/**
 * Reads the event dialog. Title, subtitle, a category and every date but the early signup are
 * required; the event cannot end before it starts, signup must open before the event ends and
 * close after it opens and no later than the event's last day, and early signup must open before
 * the general signup. Event days are sent
 * as ISO days, signup moments as ISO instants and an empty description as an empty rich-text
 * document. The thumbnail is resolved separately when saving.
 */
export function readEventDraft(
  draft: EventDraft,
): FormReading<EventField, Omit<EventInput, 'thumbnailId'>> {
  const problems = readProblems(draft)
  const { startsAt, endsAt, earlySignupStartsAt, signupStartsAt, signupEndsAt } = draft
  if (
    Object.keys(problems).length > 0 ||
    !startsAt ||
    !endsAt ||
    !signupStartsAt ||
    !signupEndsAt
  ) {
    return { problems, value: null }
  }

  return {
    problems,
    value: {
      title: draft.title.trim(),
      subtitle: draft.subtitle.trim(),
      description: draft.description.trim() ? draft.description : EMPTY_DOC_JSON,
      startsAt: toDateOnly(startsAt),
      endsAt: toDateOnly(endsAt),
      earlySignupStartsAt: earlySignupStartsAt?.toISOString() ?? null,
      signupStartsAt: signupStartsAt.toISOString(),
      signupEndsAt: signupEndsAt.toISOString(),
      categoryIds: [...draft.categoryIds],
      terms: draft.termsDocumentIds.map((documentId) => ({
        documentId,
        required: draft.termsRequired[documentId] ?? true,
      })),
    },
  }
}
