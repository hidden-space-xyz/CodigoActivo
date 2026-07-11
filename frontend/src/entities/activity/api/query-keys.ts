export const activityQueryKeys = {
  adminByEvent: (eventId: string) => ['activities', 'event', eventId] as const,
  publicByEvent: (eventId: string) => ['public', 'event-activities', eventId] as const,
  myAssignments: () => ['public', 'my-assignments'] as const,
  householdMembers: () => ['public', 'my-children'] as const,
  signupRoles: (userId: string) => ['public', 'signup-roles', userId] as const,
  householdAssignments: (eventId: string) => ['public', 'household-assignments', eventId] as const,
}
