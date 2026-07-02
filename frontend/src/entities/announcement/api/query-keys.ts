export const announcementQueryKeys = {
  all: ['announcements'] as const,
  publicDetail: (id: string) => ['announcements', 'public', id] as const,
  years: () => ['announcements', 'years'] as const,
  byYear: (year: string) => ['announcements', 'year', year] as const,
  home: () => ['announcements', 'home'] as const,
}
