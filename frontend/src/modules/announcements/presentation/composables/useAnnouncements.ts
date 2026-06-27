import { useQuery } from '@tanstack/vue-query'

import { getAnnouncements } from '@/modules/announcements/application/use-cases/get-announcements.use-case'
import { announcementRepository } from '@/modules/announcements/infrastructure/repositories/announcement-repository.provider'

const announcementQueryKeys = {
  all: ['announcements', 'public'] as const,
}

export function useAnnouncements() {
  const query = useQuery({
    queryKey: announcementQueryKeys.all,
    queryFn: () => getAnnouncements(announcementRepository),
  })

  return {
    announcements: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
  }
}
