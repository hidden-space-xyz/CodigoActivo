export const eventQueryKeys = {
  all: ['events'] as const,
  upcoming: () => [...eventQueryKeys.all, 'upcoming'] as const,
  board: () => [...eventQueryKeys.all, 'board'] as const,
  pastYears: () => [...eventQueryKeys.all, 'past-years'] as const,
  past: (year: string) => [...eventQueryKeys.all, 'past', year] as const,
  detail: (id: string) => [...eventQueryKeys.all, 'detail', id] as const,
  adminDetail: (id: string) => [...eventQueryKeys.all, 'admin-detail', id] as const,
}
