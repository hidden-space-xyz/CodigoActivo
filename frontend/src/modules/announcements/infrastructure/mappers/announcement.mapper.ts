import type { Announcement } from '@/modules/announcements/domain/entities/announcement.entity'
import type { AnnouncementResponse } from '@/shared/api/generated/models'
import { formatDate } from '@/shared/utils/format'

export function toAnnouncement(response: AnnouncementResponse): Announcement {
  return {
    id: response.id ?? '',
    title: response.title ?? '',
    subtitle: response.subtitle ?? '',
    description: response.description ?? '',
    date: response.createdAt ? formatDate(response.createdAt) : '',
    thumbnailId: response.thumbnailId ?? '',
  }
}
