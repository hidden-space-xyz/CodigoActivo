export const eventQueryKeys = {
  all: ['events'] as const,
  upcoming: () => [...eventQueryKeys.all, 'upcoming'] as const,
  board: () => [...eventQueryKeys.all, 'board'] as const,
  pastYears: () => [...eventQueryKeys.all, 'past-years'] as const,
  past: (year: string) => [...eventQueryKeys.all, 'past', year] as const,
  detail: (id: string) => [...eventQueryKeys.all, 'detail', id] as const,
  // The admin read caches the raw EventResponse, the public read a mapped EventDetail —
  // separate keys so the two shapes never share a cache entry.
  adminDetail: (id: string) => [...eventQueryKeys.all, 'admin-detail', id] as const,
}
