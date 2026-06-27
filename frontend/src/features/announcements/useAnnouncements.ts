import {
  deleteApiAnnouncementsAnnouncementId,
  getApiAnnouncements,
  getApiAnnouncementsAnnouncementId,
  patchApiAnnouncementsAnnouncementIdFeature,
  postApiAnnouncements,
  putApiAnnouncementsAnnouncementId,
} from '@/shared/api/generated/endpoints/announcements/announcements'
import { queryKeys } from '@/shared/api/query-keys'
import { useContentEntity } from '@/features/content/useContentEntity'

export function useAnnouncements() {
  return useContentEntity({
    queryKey: queryKeys.announcements,
    fetchAll: (signal) => getApiAnnouncements({ signal }).then((r) => r.data ?? []),
    fetchOne: (id) => getApiAnnouncementsAnnouncementId(id).then((r) => r.data),
    create: (body) => postApiAnnouncements(body),
    update: (id, body) => putApiAnnouncementsAnnouncementId(id, body),
    remove: (id) => deleteApiAnnouncementsAnnouncementId(id),
    feature: (id) => patchApiAnnouncementsAnnouncementIdFeature(id),
  })
}
