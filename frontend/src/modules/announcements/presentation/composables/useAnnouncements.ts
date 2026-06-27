import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { getAnnouncementById } from '@/modules/announcements/application/use-cases/get-announcement-by-id.use-case'
import { getAnnouncements } from '@/modules/announcements/application/use-cases/get-announcements.use-case'
import { announcementRepository } from '@/modules/announcements/infrastructure/repositories/announcement-repository.provider'

const announcementQueryKeys = {
  all: ['announcements', 'public'] as const,
  detail: (id: string) => ['announcements', 'public', id] as const,
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

export function useAnnouncementDetail(announcementId: MaybeRefOrGetter<string>) {
  const id = computed(() => toValue(announcementId))

  const query = useQuery({
    queryKey: computed(() => announcementQueryKeys.detail(id.value)),
    queryFn: () => getAnnouncementById(announcementRepository, id.value),
  })

  const notFound = computed(() => !query.isLoading.value && query.data.value === null)

  return {
    announcement: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    notFound,
  }
}
