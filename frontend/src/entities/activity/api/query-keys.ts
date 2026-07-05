// Activity data is cached by both the admin activities feature and the public event page, and
// some mutations invalidate across that boundary (e.g. an admin edit refreshes the public list,
// an account change refreshes the household members). Centralizing the keys here keeps those
// cross-slice invalidations in sync by construction instead of by duplicated literals.
export const activityQueryKeys = {
  /** Admin list of an event's activities (raw responses). */
  adminByEvent: (eventId: string) => ['activities', 'event', eventId] as const,
  /** Public event page's activity list (mapped view models). */
  publicByEvent: (eventId: string) => ['public', 'event-activities', eventId] as const,
  myAssignments: () => ['public', 'my-assignments'] as const,
  householdMembers: () => ['public', 'my-children'] as const,
  householdAssignments: (eventId: string) => ['public', 'household-assignments', eventId] as const,
}
