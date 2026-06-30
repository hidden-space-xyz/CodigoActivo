export const announcementQueryKeys = {
  all: ['announcements'] as const,
  public: ['announcements', 'public'] as const,
  publicDetail: (id: string) => ['announcements', 'public', id] as const,
}
