export const activityQueryKeys = {
  all: ['activities'] as const,
  adminTable: () => [...activityQueryKeys.all, 'admin-table'] as const,
  eventOptions: (eventId: string) => [...activityQueryKeys.all, 'event-options', eventId] as const,
  publicByEvent: (eventId: string) =>
    [...activityQueryKeys.all, 'public-by-event', eventId] as const,
  myAssignments: (eventId: string) =>
    [...activityQueryKeys.all, 'my-assignments', eventId] as const,
  householdMembers: () => [...activityQueryKeys.all, 'household-members'] as const,
  signupRoles: () => [...activityQueryKeys.all, 'signup-roles'] as const,
  householdAssignments: (eventId: string) =>
    [...activityQueryKeys.all, 'household-assignments', eventId] as const,
}
