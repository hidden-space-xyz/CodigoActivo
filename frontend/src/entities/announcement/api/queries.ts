import { computed, toValue, type MaybeRefOrGetter } from 'vue'
import { useQuery } from '@tanstack/vue-query'

import { announcementQueryKeys } from './query-keys'
import { getAnnouncementByIdRequest, getAnnouncementsRequest } from './requests'

export function useAnnouncements() {
  const query = useQuery({
    queryKey: announcementQueryKeys.public,
    queryFn: () => getAnnouncementsRequest(),
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
    queryKey: computed(() => announcementQueryKeys.publicDetail(id.value)),
    queryFn: () => getAnnouncementByIdRequest(id.value),
  })

  const notFound = computed(() => !query.isLoading.value && query.data.value === null)

  return {
    announcement: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    notFound,
  }
}
