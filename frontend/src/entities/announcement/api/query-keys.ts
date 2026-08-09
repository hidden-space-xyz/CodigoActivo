export const announcementQueryKeys = {
  all: ['announcements'] as const,
  publicDetail: (id: string) => [...announcementQueryKeys.all, 'public', id] as const,
  years: () => [...announcementQueryKeys.all, 'years'] as const,
  byYear: (year: string) => [...announcementQueryKeys.all, 'year', year] as const,
  home: () => [...announcementQueryKeys.all, 'home'] as const,
}
