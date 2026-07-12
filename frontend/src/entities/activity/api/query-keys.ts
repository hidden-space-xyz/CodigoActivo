export const activityQueryKeys = {
  publicByEvent: (eventId: string) => ['public', 'event-activities', eventId] as const,
  myAssignments: (eventId: string) => ['public', 'my-assignments', eventId] as const,
  householdMembers: () => ['public', 'my-children'] as const,
  signupRoles: (userId: string) => ['public', 'signup-roles', userId] as const,
  householdAssignments: (eventId: string) => ['public', 'household-assignments', eventId] as const,
}
