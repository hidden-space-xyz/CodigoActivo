import type { Announcement } from '@/modules/announcements/domain/entities/announcement.entity'
import type { AnnouncementResponse } from '@/shared/api/generated/models'
import { formatDate } from '@/shared/utils/format'

export function toAnnouncement(response: AnnouncementResponse): Announcement {
  return {
    id: response.id ?? '',
    title: response.title ?? '',
    subtitle: response.subtitle ?? '',
    date: response.createdAt ? formatDate(response.createdAt) : '',
  }
}
