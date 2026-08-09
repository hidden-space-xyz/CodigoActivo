export const eventQueryKeys = {
  all: ['events'] as const,
  upcoming: () => [...eventQueryKeys.all, 'upcoming'] as const,
  board: () => [...eventQueryKeys.all, 'board'] as const,
  pastYears: () => [...eventQueryKeys.all, 'past-years'] as const,
  past: (year: string) => [...eventQueryKeys.all, 'past', year] as const,
  detail: (id: string) => [...eventQueryKeys.all, 'detail', id] as const,
  adminTable: () => [...eventQueryKeys.all, 'admin'] as const,
  adminDetail: (id: string) => [...eventQueryKeys.all, 'admin-detail', id] as const,
  ratings: () => [...eventQueryKeys.all, 'ratings'] as const,
}

export const eventReportQueryKeys = {
  all: ['reports'] as const,
  summary: (eventId: string) => [...eventReportQueryKeys.all, 'event-summary', eventId] as const,
  attendees: () => [...eventReportQueryKeys.all, 'event-attendees'] as const,
  badges: (eventId: string) => [...eventReportQueryKeys.all, 'event-badges', eventId] as const,
  roster: (eventId: string) => [...eventReportQueryKeys.all, 'event-roster', eventId] as const,
  dashboardAnalytics: (from: string, to: string) =>
    [...eventReportQueryKeys.all, 'dashboard-analytics', from, to] as const,
}
