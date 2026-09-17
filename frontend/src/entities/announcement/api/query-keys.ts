/** Query keys for public announcement data; `byYear` also varies by the search text. */
export const announcementQueryKeys = {
  all: ['announcements'] as const,
  publicDetail: (id: string) => [...announcementQueryKeys.all, 'public', id] as const,
  years: () => [...announcementQueryKeys.all, 'years'] as const,
  byYear: (year: string, search: string) =>
    [...announcementQueryKeys.all, 'year', year, search] as const,
  home: () => [...announcementQueryKeys.all, 'home'] as const,
}
