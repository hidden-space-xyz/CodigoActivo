import type { ActivityDetail, ActivityInput, ActivityRole } from '@/entities/activity'
import { isDayAfter, isDayBefore } from '@/shared/lib/date'
import type { FormProblem, FormReading } from '@/shared/lib/form'

/** What the activity dialog binds its inputs to; each date picker holds `null` until one is picked. */
export interface ActivityDraft {
  title: string
  description: string
  location: string
  modalityId: string
  startsAt: Date | null
  endsAt: Date | null
  /** Desired signups by role id; a role without a count has no target. */
  desiredCounts: Record<string, number | undefined>
}

/** Days of the event the activity belongs to, as ISO days or instants; `null` while unknown. */
export interface EventDays {
  readonly startsAt: string | null
  readonly endsAt: string | null
}

/** Field of the activity form that can be refused; `schedule` stands for both dates at once. */
type ActivityField =
  'title' | 'description' | 'location' | 'modalityId' | 'startsAt' | 'endsAt' | 'schedule'

/** Desired counts of every role, taken from the activity's saved targets. */
export function toDesiredCounts(
  activity: ActivityDetail | null,
  roles: readonly ActivityRole[],
): Record<string, number | undefined> {
  const saved = new Map(
    (activity?.roleCapacities ?? []).map((capacity) => [
      capacity.roleTypeId,
      capacity.desiredCount,
    ]),
  )
  return Object.fromEntries(roles.map((role) => [role.id, saved.get(role.id)]))
}

/** A blank draft, or one filled with the activity being edited. */
export function toActivityDraft(
  activity: ActivityDetail | null,
  roles: readonly ActivityRole[],
): ActivityDraft {
  return {
    title: activity?.title ?? '',
    description: activity?.description ?? '',
    location: activity?.location ?? '',
    modalityId: activity?.modalityId ?? '',
    startsAt: activity ? new Date(activity.startsAt) : null,
    endsAt: activity ? new Date(activity.endsAt) : null,
    desiredCounts: toDesiredCounts(activity, roles),
  }
}

/** Whether a day lies outside the event, so the date pickers refuse it. */
export function isOutsideEvent(date: Date, event: EventDays): boolean {
  return isDayBefore(date, event.startsAt) || isDayAfter(date, event.endsAt)
}

function readProblems(
  draft: ActivityDraft,
  event: EventDays,
): Partial<Record<ActivityField, FormProblem>> {
  const { startsAt, endsAt } = draft
  const problems: Partial<Record<ActivityField, FormProblem>> = {}
  if (!draft.title.trim()) problems.title = true
  if (!draft.description.trim()) problems.description = true
  if (!draft.location.trim()) {
    problems.location = 'pages.admin.eventDetail.activities.form.problems.locationRequired'
  }
  if (!draft.modalityId) {
    problems.modalityId = 'pages.admin.eventDetail.activities.form.problems.modalityRequired'
  }
  const outside =
    !!startsAt &&
    !!endsAt &&
    (isDayBefore(startsAt, event.startsAt) || isDayAfter(endsAt, event.endsAt))
  if (outside) {
    problems.schedule = 'pages.admin.eventDetail.activities.form.problems.outsideEvent'
    problems.startsAt = true
    problems.endsAt = true
  }
  if (!startsAt)
    problems.startsAt = 'pages.admin.eventDetail.activities.form.problems.startRequired'
  if (!endsAt) problems.endsAt = 'pages.admin.eventDetail.activities.form.problems.endRequired'
  else if (startsAt && endsAt <= startsAt) {
    problems.endsAt = 'pages.admin.eventDetail.activities.form.problems.orderInvalid'
  }
  return problems
}

/**
 * Reads the activity dialog. Every field is required, the activity must end after it starts and
 * stay within the event's days; roles without a desired count of at least one are left out. The
 * thumbnail is resolved separately when saving.
 */
export function readActivityDraft(
  draft: ActivityDraft,
  event: EventDays,
): FormReading<ActivityField, Omit<ActivityInput, 'thumbnailId'>> {
  const problems = readProblems(draft, event)
  const { startsAt, endsAt } = draft
  if (Object.keys(problems).length > 0 || !startsAt || !endsAt) return { problems, value: null }

  return {
    problems,
    value: {
      title: draft.title.trim(),
      description: draft.description.trim(),
      location: draft.location.trim(),
      modalityId: draft.modalityId,
      startsAt: startsAt.toISOString(),
      endsAt: endsAt.toISOString(),
      roleCapacities: Object.entries(draft.desiredCounts).flatMap(([roleTypeId, desiredCount]) =>
        typeof desiredCount === 'number' && desiredCount >= 1 ? [{ roleTypeId, desiredCount }] : [],
      ),
    },
  }
}
