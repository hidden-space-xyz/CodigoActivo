import type {
  AnnouncementListItemResponse,
  AnnouncementResponse,
} from '@/shared/api/generated/models'
import { formatDate } from '@/shared/lib'

import type { Announcement, AnnouncementSummary } from '../model/types'

export function toAnnouncementSummary(response: AnnouncementListItemResponse): AnnouncementSummary {
  return {
    id: response.id ?? '',
    title: response.title ?? '',
    subtitle: response.subtitle ?? '',
    date: response.createdAt ? formatDate(response.createdAt) : '',
    thumbnailId: response.thumbnailId ?? '',
    featured: response.featured ?? false,
  }
}

export function toAnnouncement(response: AnnouncementResponse): Announcement {
  return {
    ...toAnnouncementSummary(response),
    description: response.description ?? '',
  }
}
