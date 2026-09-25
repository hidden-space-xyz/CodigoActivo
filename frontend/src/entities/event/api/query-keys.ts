/**
 * Query keys for public and admin event data. Every key starts with `all`, so invalidating
 * `eventQueryKeys.all` refreshes every event query at once.
 */
export const eventQueryKeys = {
  all: ['events'] as const,
  upcoming: () => [...eventQueryKeys.all, 'upcoming'] as const,
  board: () => [...eventQueryKeys.all, 'board'] as const,
  pastYears: () => [...eventQueryKeys.all, 'past-years'] as const,
  pastCategories: () => [...eventQueryKeys.all, 'past-categories'] as const,
  past: (year: string, search: string, categoryId: string) =>
    [...eventQueryKeys.all, 'past', year, search, categoryId] as const,
  detail: (id: string) => [...eventQueryKeys.all, 'detail', id] as const,
  terms: (id: string) => [...eventQueryKeys.all, 'terms', id] as const,
  leaderRoster: (id: string, userId: string | null) =>
    [...eventQueryKeys.all, 'leader-roster', id, userId] as const,
  adminTable: () => [...eventQueryKeys.all, 'admin'] as const,
  adminDetail: (id: string) => [...eventQueryKeys.all, 'admin-detail', id] as const,
  ratings: () => [...eventQueryKeys.all, 'ratings'] as const,
}

/**
 * Query keys for admin event reports and dashboard analytics, kept under a separate `reports` root
 * so activity and assignment mutations can refresh reports without touching event lists.
 */
export const eventReportQueryKeys = {
  all: ['reports'] as const,
  summary: (eventId: string) => [...eventReportQueryKeys.all, 'event-summary', eventId] as const,
  attendees: () => [...eventReportQueryKeys.all, 'event-attendees'] as const,
  badges: (eventId: string) => [...eventReportQueryKeys.all, 'event-badges', eventId] as const,
  roster: (eventId: string) => [...eventReportQueryKeys.all, 'event-roster', eventId] as const,
  dashboardAnalytics: (from: string, to: string) =>
    [...eventReportQueryKeys.all, 'dashboard-analytics', from, to] as const,
}
